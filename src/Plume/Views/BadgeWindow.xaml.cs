using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Plume.Models;
// Alias locaux : lève l'ambiguïté WPF / WinForms (ImplicitUsings + UseWindowsForms importent les deux),
// cf. GlobalUsings.cs pour Application/Clipboard/MessageBox — ceux-ci sont propres à cette fenêtre.
using Point = System.Windows.Point;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;

namespace Plume.Views;

public partial class BadgeWindow : Window
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_NOACTIVATE = 0x08000000;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    private const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
    private const uint WINEVENT_OUTOFCONTEXT = 0x0000;
    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002, SWP_NOSIZE = 0x0001, SWP_NOACTIVATE = 0x0010;

    private delegate void WinEventDelegate(IntPtr hook, uint evt, IntPtr hwnd, int idObject, int idChild, uint thread, uint time);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc,
        WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);
    [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private IntPtr _fgHook;
    private WinEventDelegate? _fgProc; // référence gardée : évite que le GC ramasse le delegate du hook

    private Point _dragStart;
    private bool _dragging;
    private bool _secondClick; // vrai si l'appui en cours est le 2e d'un double-clic (ClickCount == 2)

    public event Action? Clicked;
    public event Action? DoubleClicked;
    public event Action<BadgePosition>? Moved;

    /// <summary>Identifiant de l'écran pour lequel cette pastille a été créée (Screen.DeviceName). V8-J.</summary>
    public string ScreenId { get; }

    public BadgeWindow(string screenId, BadgePosition? saved,
        double workAreaLeft, double workAreaTop, double workAreaWidth, double workAreaHeight)
    {
        InitializeComponent();
        ScreenId = screenId;
        SourceInitialized += OnSourceInitialized;
        MouseLeftButtonDown += Badge_MouseLeftButtonDown;
        MouseMove += Badge_MouseMove;
        MouseLeftButtonUp += Badge_MouseLeftButtonUp;

        var (x, y) = saved is not null
            ? Plume.Services.BadgePlacementService.Clamp(saved.X, saved.Y, Width, Height,
                workAreaLeft, workAreaTop, workAreaWidth, workAreaHeight)
            : Plume.Services.BadgePlacementService.DefaultPosition(
                workAreaLeft, workAreaTop, workAreaWidth, workAreaHeight, Width, Height);
        Left = x;
        Top = y;

        // Logo Plume, remplissant tout le rond (source partagée, gelée → pas de verrou fichier).
        LogoBrush.ImageSource = Plume.Services.AppLogo.Image;
    }

    /// <summary>Ramène la pastille dans la zone de travail donnée si elle en dépasse (écran redimensionné,
    /// débranché puis rebranché…). V8-J.</summary>
    public void Reclamp(double workAreaLeft, double workAreaTop, double workAreaWidth, double workAreaHeight)
    {
        var (x, y) = Plume.Services.BadgePlacementService.Clamp(Left, Top, Width, Height,
            workAreaLeft, workAreaTop, workAreaWidth, workAreaHeight);
        Left = x;
        Top = y;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW);

        // Ré-affirme le topmost à chaque changement de premier plan (certaines fenêtres topmost passeraient devant).
        _fgProc = (_, _, _, _, _, _, _) =>
        {
            if (!IsVisible) return;
            var h = new WindowInteropHelper(this).Handle;
            if (h != IntPtr.Zero) SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        };
        _fgHook = SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, _fgProc, 0, 0, WINEVENT_OUTOFCONTEXT);
        Closed += (_, _) => { if (_fgHook != IntPtr.Zero) { UnhookWinEvent(_fgHook); _fgHook = IntPtr.Zero; } };
    }

    private void Badge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = PointToScreen(e.GetPosition(this));
        _dragging = false;
        _secondClick = e.ClickCount == 2;
        CaptureMouse();
    }

    private void Badge_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        var now = PointToScreen(e.GetPosition(this));
        var dx = now.X - _dragStart.X;
        var dy = now.Y - _dragStart.Y;
        if (!_dragging && Math.Abs(dx) + Math.Abs(dy) < 4) return; // seuil clic vs glisser
        _dragging = true;
        Left += dx;
        Top += dy;
        _dragStart = now;
    }

    private void Badge_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ReleaseMouseCapture();
        if (_dragging) Moved?.Invoke(new BadgePosition { X = Left, Y = Top });
        else if (_secondClick) DoubleClicked?.Invoke(); // un glisser n'émet jamais DoubleClicked
        else Clicked?.Invoke();
        _dragging = false;
        _secondClick = false;
    }

    private void Card_MouseEnter(object sender, MouseEventArgs e)
    {
        AnimateHover(1.22, 1.0);
        if (_state == BadgeState.Listening) { ListenBars.Visibility = Visibility.Collapsed; ListenStop.Visibility = Visibility.Visible; }
    }

    private void Card_MouseLeave(object sender, MouseEventArgs e)
    {
        AnimateHover(1.0, 0.85);
        ListenBars.Visibility = Visibility.Visible; ListenStop.Visibility = Visibility.Collapsed;
    }

    /// <summary>Au survol, la pastille grossit et devient pleinement opaque ; retour à la normale à la sortie.</summary>
    private void AnimateHover(double scale, double opacity)
    {
        var dur = new System.Windows.Duration(TimeSpan.FromMilliseconds(120));
        var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        var sx = new System.Windows.Media.Animation.DoubleAnimation(scale, dur) { EasingFunction = ease };
        var sy = new System.Windows.Media.Animation.DoubleAnimation(scale, dur) { EasingFunction = ease };
        var op = new System.Windows.Media.Animation.DoubleAnimation(opacity, dur) { EasingFunction = ease };
        CardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleXProperty, sx);
        CardScale.BeginAnimation(System.Windows.Media.ScaleTransform.ScaleYProperty, sy);
        Card.BeginAnimation(OpacityProperty, op);
    }

    private enum BadgeState { Idle, Listening, Processing, Error }
    private BadgeState _state = BadgeState.Idle;
    private System.Windows.Media.Animation.DoubleAnimation? _spin;
    private System.Windows.Media.Effects.DropShadowEffect? _haloListen; // halo vert (écoute), créé une fois
    private System.Windows.Media.Effects.DropShadowEffect? _haloBusy;   // halo bleu (traitement), créé une fois
    private System.Windows.Media.Effects.DropShadowEffect? _haloError;  // halo rouge (erreur), créé une fois

    // Barres de niveau (écoute) : hauteurs de repos (aucun son) et lissage pour un mouvement naturel.
    private const double BarRestHeight = 8, BarMaxHeight = 24;
    private static readonly double[] BarFactor = { 0.7, 1.0, 0.8 }; // barre du milieu plus réactive
    private float _smoothedLevel;

    /// <summary>Repos : logo Plume.</summary>
    public void ShowIdle() => Dispatcher.Invoke(() => SetState(BadgeState.Idle));

    /// <summary>Écoute : 3 barres vertes (clic = stop).</summary>
    public void ShowListening() => Dispatcher.Invoke(() => SetState(BadgeState.Listening));

    /// <summary>Traitement : spinner violet.</summary>
    public void ShowProcessing() => Dispatcher.Invoke(() => SetState(BadgeState.Processing));

    /// <summary>Erreur : « ! » rouge (l'appelant le range au repos après ~2 s).</summary>
    public void ShowError() => Dispatcher.Invoke(() => SetState(BadgeState.Error));

    /// <summary>Fait bouger les 3 barres vertes selon le niveau vocal capté (pendant l'écoute uniquement).
    /// Lissage exponentiel léger pour un mouvement naturel ; aucun coût hors écoute (ignoré).</summary>
    public void SetLevel(float level) => Dispatcher.Invoke(() =>
    {
        if (_state != BadgeState.Listening) return;
        _smoothedLevel = _smoothedLevel * 0.55f + level * 0.45f;
        // Le pic brut d'une voix normale est souvent faible (< 0.3) : un gain compense pour rester lisible.
        var amount = Math.Clamp(_smoothedLevel * 3.2, 0, 1);
        var grow = BarMaxHeight - BarRestHeight;
        Bar1.Height = BarRestHeight + amount * grow * BarFactor[0];
        Bar2.Height = BarRestHeight + amount * grow * BarFactor[1];
        Bar3.Height = BarRestHeight + amount * grow * BarFactor[2];
    });

    private void SetState(BadgeState s)
    {
        _state = s;
        Glyph.Visibility       = s == BadgeState.Idle       ? Visibility.Visible : Visibility.Collapsed;
        ListenGlyph.Visibility = s == BadgeState.Listening  ? Visibility.Visible : Visibility.Collapsed;
        BusyGlyph.Visibility   = s == BadgeState.Processing ? Visibility.Visible : Visibility.Collapsed;
        ErrorGlyph.Visibility  = s == BadgeState.Error      ? Visibility.Visible : Visibility.Collapsed;
        ListenStop.Visibility  = Visibility.Collapsed;
        ListenBars.Visibility  = Visibility.Visible;

        if (s == BadgeState.Listening)
        {
            // Repos net à chaque nouvelle écoute (pas de reliquat du niveau précédent).
            _smoothedLevel = 0;
            Bar1.Height = Bar2.Height = Bar3.Height = BarRestHeight;
        }

        // Halo : vert pendant l'écoute, bleu pendant le traitement, rouge en erreur, rien au repos. Porté par l'ellipse Halo
        // (et non par Card) : les barres de niveau qui bougent ne forcent pas le recalcul du flou.
        Halo.Effect = s switch
        {
            BadgeState.Listening => _haloListen ??= MakeHalo(ThemeColor("GreenColor")),   // vert (assorti aux barres)
            BadgeState.Processing => _haloBusy ??= MakeHalo(ThemeColor("HaloBusyColor")), // bleu
            BadgeState.Error => _haloError ??= MakeHalo(ThemeColor("BadgeErrorColor")),   // rouge
            _ => null,
        };
        Halo.Visibility = s == BadgeState.Idle ? Visibility.Collapsed : Visibility.Visible;

        // Spinner : animation lancée seulement pendant le traitement (aucun coût au repos).
        if (s == BadgeState.Processing)
        {
            _spin ??= new System.Windows.Media.Animation.DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(900))
            { RepeatBehavior = System.Windows.Media.Animation.RepeatBehavior.Forever };
            SpinRot.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, _spin);
        }
        else
            SpinRot.BeginAnimation(System.Windows.Media.RotateTransform.AngleProperty, null);
    }

    /// <summary>Couleur d'un token du thème (mêmes valeurs en clair et en sombre pour la pastille : le halo
    /// mis en cache par ??= n'a pas besoin de suivre un changement de thème à chaud).</summary>
    private System.Windows.Media.Color ThemeColor(string key)
        => TryFindResource(key) is System.Windows.Media.Color c ? c : System.Windows.Media.Colors.Gray;

    /// <summary>Lueur douce autour de la pastille (halo d'état).</summary>
    private static System.Windows.Media.Effects.DropShadowEffect MakeHalo(System.Windows.Media.Color c) => new()
    {
        Color = c,
        ShadowDepth = 0,
        BlurRadius = 16,
        Opacity = 0.95,
    };
}
