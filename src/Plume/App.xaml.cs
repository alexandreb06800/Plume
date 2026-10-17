using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Plume.Models;
using Plume.Services;
using Plume.Views;

namespace Plume;

public partial class App : Application
{
    private static Mutex? _mutex;
    private TrayIcon? _tray;
    private ConfigService? _configService;
    private AppConfig? _config;
    private HotkeyService? _hotkeyService;
    private DictationController? _controller;
    private SettingsWindow? _settingsWindow;
    private OnboardingWindow? _onboarding;
    private ThemeService? _theme;
    private readonly List<BadgeWindow> _badges = new();
    private bool _displaySubscribed;
    private bool _listening;
    // Minuteur simple clic vs double-clic : le menu des styles ne s'ouvre qu'après le délai système de
    // double-clic, laissant sa chance au 2e clic (démarrage de la dictée). Un seul minuteur pour toutes
    // les pastilles (un seul curseur, donc une seule séquence de clics à la fois).
    private System.Windows.Threading.DispatcherTimer? _clickTimer;
    private System.Windows.Threading.DispatcherTimer? _errorTimer; // retour au repos ~2 s après une erreur
    private DictationVisual _lastVisual = DictationVisual.Idle;     // dernier état reçu

    /// <summary>Service de thème, exposé pour les fenêtres à chrome standard (barre de titre sombre).</summary>
    public ThemeService? Theme => _theme;

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetDoubleClickTime();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Filet de sécurité global : aucune exception imprévue ne doit tuer le process (règle « jamais de
        // crash »). On journalise (si le diag est actif) et on continue — l'état de la dictée retombe sur
        // Idle par les finally des flux. Complément défensif, pas un substitut à la gestion locale.
        DispatcherUnhandledException += (_, ex) =>
        {
            try
            {
                _ = new DiagLogService().Log(_config?.DiagnosticsLogEnabled ?? false,
                    "Exception non gérée", $"{ex.Exception.GetType().Name} : {ex.Exception.Message}");
            }
            catch { }
            ex.Handled = true;
        };

        // Complément au filet ci-dessus : couvre les exceptions hors thread UI (tâche d'arrière-plan,
        // callback de rendu). Le process ne survit pas dans ce cas (le CLR l'a déjà décidé), mais la
        // trace journalisée permet de diagnostiquer un crash a posteriori (cf. Plume.exe/PresentationCore.dll
        // du 2026-07-17, sans trace exploitable faute de ce journal).
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
        {
            try
            {
                // Écriture synchrone : le processus va s'arrêter, une écriture en tâche de fond n'aurait pas le temps.
                new DiagLogService().LogNow(_config?.DiagnosticsLogEnabled ?? false,
                    "Exception fatale (AppDomain)", $"{ex.ExceptionObject}");
            }
            catch { }
        };
        TaskScheduler.UnobservedTaskException += (_, ex) =>
        {
            try
            {
                _ = new DiagLogService().Log(_config?.DiagnosticsLogEnabled ?? false,
                    "Exception de tâche non observée", $"{ex.Exception.GetType().Name} : {ex.Exception.Message}");
            }
            catch { }
            ex.SetObserved();
        };

        _theme = new ThemeService();

        _mutex = new Mutex(true, "Plume_SingleInstance_Mutex", out bool isNew);
        if (!isNew)
        {
            // Une instance tourne déjà : on sort sans rien afficher.
            Shutdown();
            return;
        }

        // Ancien exe laissé par une mise à jour : supprimé en tâche de fond (jamais sur le chemin du démarrage).
        _ = Task.Run(() => UpdateService.CleanupOld(AppContext.BaseDirectory));

        _configService = new ConfigService();
        bool premierLancement = !System.IO.File.Exists(_configService.ConfigPath);
        _config = _configService.Load();

        // Thème (clair/sombre) appliqué avant toute fenêtre, selon le réglage (auto = suit Windows à chaud).
        _theme.Apply(_config.ThemeMode);

        // 1er lancement : Plume démarre avec Windows par défaut (toggle sur Oui).
        if (premierLancement)
        {
            try
            {
                _config.StartWithWindows = true;
                _configService.Save(_config);
            }
            catch { /* disque indisponible : on ne bloque jamais le démarrage */ }
        }

        // Auto-réparation du « Démarrer avec Windows » : si l'option est active, on réinscrit l'entrée Run
        // à chaque lancement — elle peut avoir été retirée (synchro OneDrive, sécurité, nettoyage) ou pointer
        // vers un chemin périmé. SetEnabled écrit toujours un chemin stable (exe installé si l'instance
        // courante tourne depuis OneDrive). Coût : une écriture registre, négligeable.
        if (_config.StartWithWindows)
        {
            try { StartupService.SetEnabled(true, Environment.ProcessPath ?? ""); }
            catch { /* registre indisponible : on ne bloque jamais le démarrage */ }
        }

        _hotkeyService = new HotkeyService();

        _tray = new TrayIcon(
            onToggleDictation: () => _controller?.ToggleDictation(),
            onOpenSettings: OpenSettings,
            onQuit: Shutdown,
            isStartupEnabled: StartupService.IsEnabled,
            onSetStartup: enabled =>
            {
                StartupService.SetEnabled(enabled, Environment.ProcessPath ?? "");
                _config!.StartWithWindows = enabled;
                _configService!.Save(_config);
            },
            getLastText: () => _controller?.LastText);

        _controller = new DictationController(_configService, () => _config!, _tray, _hotkeyService);
        _controller.OpenSettingsRequested += OpenSettings;
        // Retour visuel unique de la dictée (V7) : la pastille reflète Idle/Listening/Processing.
        _controller.VisualChanged += v => Dispatcher.BeginInvoke(new Action(() => OnDictationVisual(v)));
        // Barres vertes animées selon le niveau vocal capté (pendant l'écoute uniquement, ignoré sinon).
        // Le niveau arrive sur le thread de capture NAudio : marshaler AVANT d'énumérer _badges, que le
        // thread UI peut modifier au même moment (écran branché/débranché pendant une dictée).
        _controller.LevelChanged += level => Dispatcher.BeginInvoke(new Action(() =>
        {
            foreach (var b in _badges) b.SetLevel(level);
        }));

        // Dictée : un seul raccourci global.
        _hotkeyService.DictationHotkeyPressed += vk =>
            Application.Current.Dispatcher.Invoke(() => _controller!.ToggleDictation(vk));

        // Assistant vocal (V4-C) : raccourci global dédié.
        _hotkeyService.AgentHotkeyPressed += vk =>
            Application.Current.Dispatcher.Invoke(() => _controller!.StartAgent(vk));

        RegisterHotkeys();

        // Pastilles flottantes créées en différé (jamais au boot → ouverture < 1 s préservée).
        // Créées même quand la pastille est masquée : ReconcileBadges ne les affiche pas (garde ShowBadge),
        // mais leur coût de construction (BAML, décodage du logo, fenêtre en couches, × nombre d'écrans) est
        // ainsi payé au repos et non sur le chemin critique du PREMIER appui — la pastille étant le seul
        // retour visuel, ce réglage en faisait sinon l'opération la plus lourde de ce chemin.
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(CreateBadges));

        // Démarrage avec Windows peu fiable si Plume tourne depuis OneDrive : on prévient.
        if (StartupService.IsRunningFromOneDrive())
            _tray.ShowBalloon("Plume",
                "Plume est lancé depuis OneDrive : le « Démarrer avec Windows » peut être instable. "
                + "Place Plume dans un dossier local (hors OneDrive).");

        // Réglages illisibles (après la bulle OneDrive, pour ne pas être remplacée par elle).
        if (_configService.RecoveryBackupPath is { } copie)
            _tray.ShowBalloon("Plume",
                $"Réglages illisibles : Plume repart des réglages par défaut. L'ancien fichier est gardé ({System.IO.Path.GetFileName(copie)}).");
        else if (_configService.SavesSuspended)
            _tray.ShowBalloon("Plume",
                "Réglages illisibles pour le moment : aucun changement ne sera enregistré avant le prochain démarrage de Plume.");

        // Aucune clé configurée : premier contact — assistant de prise en main (V8-B). Une clé
        // présente = plus jamais affiché. Fenêtre créée à la demande uniquement : le démarrage
        // normal ne charge rien de plus (RÈGLE PRIMORDIALE).
        if (_configService.ResolveApiKey(_config) is null && _configService.ResolveGroqKey(_config) is null)
            OpenOnboarding();
    }

    private void OpenSettings()
    {
        // Si une fenêtre Réglages est déjà ouverte, on la ramène au premier plan.
        if (_settingsWindow is not null)
        {
            if (_settingsWindow.WindowState == WindowState.Minimized)
                _settingsWindow.WindowState = WindowState.Normal;
            _settingsWindow.Activate();
            return;
        }

        var win = new SettingsWindow(_configService!, _config!,
                                     onSaved: cfg => { _config = cfg; RegisterHotkeys(); _theme?.Apply(cfg.ThemeMode); ApplyBadgeVisibility(); _tray?.RefreshStartup(cfg.StartWithWindows); },
                                     getLastText: () => _controller?.LastText);
        win.Closed += (_, _) => _settingsWindow = null; // libère la référence à la fermeture
        _settingsWindow = win;
        win.Show();
        win.Activate();
    }

    /// <summary>Ouvre l'assistant de prise en main (premier lancement sans clé, ou À propos →
    /// « Revoir la prise en main »). Fenêtre unique : un second appel ramène l'existante au premier plan.</summary>
    public void OpenOnboarding()
    {
        if (_onboarding is not null)
        {
            _onboarding.Activate();
            return;
        }
        var win = new OnboardingWindow(_configService!, _config!);
        win.Closed += (_, _) =>
        {
            _onboarding = null; // libère la référence à la fermeture
            // L'onboarding a pu basculer CleanupModel vers un modèle Groq (config 100 % Groq). Les Réglages,
            // s'ils sont restés ouverts derrière, doivent relire la config : leur combo périmé réécraserait
            // sinon la bascule au prochain enregistrement, rebloquant la première dictée.
            _settingsWindow?.RefreshModelCombos();
        };
        _onboarding = win;
        win.Show();
        win.Activate();
    }

    /// <summary>Relance Plume après une mise à jour installée. Le verrou d'instance unique est rendu AVANT de
    /// lancer le nouvel exe, sinon celui-ci se croirait en double et quitterait aussitôt.</summary>
    public void RestartAfterUpdate()
    {
        var exe = Environment.ProcessPath;
        try { _mutex?.ReleaseMutex(); } catch { /* verrou non détenu par ce thread : le nouvel exe attendra la sortie */ }
        try { _mutex?.Dispose(); } catch { }
        _mutex = null;
        if (!string.IsNullOrEmpty(exe))
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true }); }
            catch { /* relance impossible : l'utilisateur relancera Plume à la main, la nouvelle version est en place */ }
        }
        Shutdown();
    }

    /// <summary>(Ré)enregistre le raccourci de dictée et celui de l'assistant vocal (rebind à chaud).</summary>
    private void RegisterHotkeys()
    {
        if (_hotkeyService is null || _config is null) return;

        var dictErr = _hotkeyService.RegisterDictationHotkey(_config.Hotkey);
        if (dictErr is not null)
            _tray?.ShowBalloon("Plume", $"Raccourci de dictée : {dictErr}");

        _hotkeyService.UnregisterAgentHotkey();
        if (_config.AgentEnabled && _config.AgentHotkey is not null)
        {
            var agentErr = _hotkeyService.RegisterAgentHotkey(_config.AgentHotkey);
            if (agentErr is not null)
                _tray?.ShowBalloon("Plume", $"Raccourci de l'assistant vocal : {agentErr}");
        }
    }

    /// <summary>Crée une pastille par écran connecté et s'abonne aux changements d'écrans (V8-J).
    /// Appelée en différé (jamais au boot) ; idempotente.</summary>
    private void CreateBadges()
    {
        if (_config is null || _controller is null) return;
        // Idempotent : évite un double abonnement si CreateBadges est rappelée (ré-affichage des pastilles).
        _controller.ListeningChanged -= OnListeningChanged;
        _controller.ListeningChanged += OnListeningChanged;
        if (!_displaySubscribed)
        {
            SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            _displaySubscribed = true;
        }
        ReconcileBadges();
    }

    /// <summary>Réconcilie la liste des pastilles avec les écrans actuellement connectés : crée celles des
    /// nouveaux écrans, ferme celles des écrans disparus, replace toutes les autres dans leur zone visible
    /// (« jamais hors écran »). V8-J.</summary>
    private void ReconcileBadges()
    {
        if (_config is null) return;
        var screens = Win32Screens.All();
        if (!_config.BadgeOnAllScreens)
        {
            var primary = screens.FirstOrDefault(s => s.IsPrimary) ?? screens.FirstOrDefault();
            screens = primary is null ? new List<ScreenInfo>() : new List<ScreenInfo> { primary };
        }
        var currentIds = new HashSet<string>(screens.Select(s => s.DeviceName));

        for (int i = _badges.Count - 1; i >= 0; i--)
        {
            if (!currentIds.Contains(_badges[i].ScreenId))
            {
                _badges[i].Close();
                _badges.RemoveAt(i);
            }
        }

        foreach (var screen in screens)
        {
            // DIP et non pixels physiques : Window.Left/Top est en DIP (PerMonitorV2) — sans conversion,
            // un écran à 125/150 % placerait la pastille hors de la zone visible réelle.
            var (areaLeft, areaTop, areaWidth, areaHeight) = ScreenMetrics.WorkAreaDip(screen);
            var badge = _badges.FirstOrDefault(b => b.ScreenId == screen.DeviceName);
            if (badge is null)
            {
                var saved = _config.BadgePositions.TryGetValue(screen.DeviceName, out var pos) ? pos : null;
                badge = new BadgeWindow(screen.DeviceName, saved, areaLeft, areaTop, areaWidth, areaHeight);
                badge.Clicked += () => OnBadgeClicked(badge);
                badge.DoubleClicked += () => OnBadgeDoubleClicked();
                badge.Moved += p => SaveBadgePosition(p);
                _badges.Add(badge);
                if (_config.ShowBadge) badge.Show();
            }
            else
            {
                badge.Reclamp(areaLeft, areaTop, areaWidth, areaHeight);
            }
        }
    }

    /// <summary>Écran ajouté/retiré ou zone de travail modifiée (résolution, barre des tâches déplacée) :
    /// réconcilie les pastilles. Marshalé sur le thread UI (l'événement arrive sur le thread interne de
    /// SystemEvents, même motif que ThemeService).</summary>
    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.Invoke(ReconcileBadges);

    /// <summary>Détermine l'écran sous la position déposée (peut différer de l'écran d'origine si la
    /// pastille a été glissée sur un autre écran) et y mémorise la position. V8-J.</summary>
    private void SaveBadgePosition(BadgePosition pos)
    {
        if (_config is null) return;
        // pos est en DIP (Window.Left/Top) : résoudre l'écran via la conversion, pas Win32Screens.FromPoint
        // directement (qui attend des pixels physiques et rendrait le mauvais écran sur les setups à DPI mixte).
        var screen = ScreenMetrics.ScreenFromDip(pos.X, pos.Y);
        _config.BadgePositions[screen.DeviceName] = pos;
        try { _configService!.Save(_config); } catch { }
    }

    /// <summary>Clic sur une pastille : arrête l'écoute en cours si le bouton Stop est affiché (immédiat,
    /// aucun conflit de geste dans cet état). Au repos, le menu des styles ne s'ouvre qu'après le délai
    /// système de double-clic : un 2e clic dans l'intervalle démarre la dictée à la place.</summary>
    private void OnBadgeClicked(BadgeWindow badge)
    {
        if (_listening) { _controller?.StopListening(); return; }

        _clickTimer?.Stop();
        _clickTimer ??= new System.Windows.Threading.DispatcherTimer();
        _clickTimer.Interval = TimeSpan.FromMilliseconds(GetDoubleClickTime());
        // Handler reposé à chaque clic (capture la pastille cliquée) ; l'ancien est désabonné avant.
        if (_clickTick is not null) _clickTimer.Tick -= _clickTick;
        _clickTick = (_, _) => { _clickTimer!.Stop(); OpenStyleMenu(badge); };
        _clickTimer.Tick += _clickTick;
        _clickTimer.Start();
    }

    private EventHandler? _clickTick; // référence gardée pour désabonner le handler du clic précédent

    /// <summary>Double-clic sur une pastille : démarre la dictée (bascule). La pastille étant
    /// WS_EX_NOACTIVATE, le premier plan est encore l'appli de l'utilisateur : on capture la fenêtre
    /// cible maintenant pour que l'insertion vise le bon endroit, sans perte de focus ni de sélection.
    /// vk = 0 → la dictée reste en bascule même en mode « maintien » (on ne maintient pas un double-clic).</summary>
    private void OnBadgeDoubleClicked()
    {
        _clickTimer?.Stop(); // annule l'ouverture différée du menu (armée par le 1er clic de la séquence)
        if (_listening) return; // sécurité : ne rien lancer si une écoute démarre entre-temps
        IntPtr target = GetForegroundWindow();
        _controller?.ToggleDictation(0, target);
    }

    /// <summary>Mémorise l'état d'écoute courant : détermine si un clic sur la pastille arrête la dictée
    /// (Stop) ou ouvre le menu des styles. Le rendu visuel de la pastille est piloté séparément par
    /// <see cref="OnDictationVisual"/> (VisualChanged).</summary>
    private void OnListeningChanged(bool listening) => _listening = listening;

    /// <summary>Reflète l'état de dictée sur la pastille. Si la pastille est masquée (réglage),
    /// elle apparaît le temps de l'activité puis se cache — le retour visuel est garanti.</summary>
    private void OnDictationVisual(DictationVisual v)
    {
        _lastVisual = v;
        _errorTimer?.Stop();
        if (v != DictationVisual.Idle && _badges.Count == 0) CreateBadges(); // création à la demande
        switch (v)
        {
            case DictationVisual.Listening:
                foreach (var b in _badges) { b.Show(); b.ShowListening(); }
                break;
            case DictationVisual.Processing:
                foreach (var b in _badges) { b.Show(); b.ShowProcessing(); }
                break;
            case DictationVisual.Error:
                foreach (var b in _badges) { b.Show(); b.ShowError(); }
                _errorTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                _errorTimer.Tick -= OnErrorTimerTick;
                _errorTimer.Tick += OnErrorTimerTick;
                _errorTimer.Start();
                break;
            default:
                foreach (var b in _badges) b.ShowIdle();
                if (_config is not null && !_config.ShowBadge)
                    foreach (var b in _badges) b.Hide(); // masquées par réglage → on les range
                break;
        }
    }

    /// <summary>Fin des ~2 s d'erreur : retour au repos, sauf si une nouvelle dictée a démarré entre-temps.</summary>
    private void OnErrorTimerTick(object? sender, EventArgs e)
    {
        _errorTimer?.Stop();
        if (_lastVisual == DictationVisual.Error) OnDictationVisual(DictationVisual.Idle);
    }

    private void OpenStyleMenu(BadgeWindow badge)
    {
        // Mémorisé dès l'entrée (la pastille est WS_EX_NOACTIVATE : au clic, le premier plan est encore
        // l'appli de l'utilisateur) — évite qu'une capture tardive via GetForegroundWindow ne vise le menu.
        IntPtr target = GetForegroundWindow();
        if (_config is null || _controller is null) return;
        var menu = new StyleMenuWindow(_config.SelectionStyles, badge.Left, badge.Top,
                                       hasLastText: !string.IsNullOrEmpty(_controller.LastText));
        menu.AgentChosen += () => _controller.StartAgent();       // assistant vocal (la dictée reste sur Ctrl+Espace)
        menu.CopyLastChosen += () => _tray?.CopyLast();
        menu.StyleChosen += style => _controller.ApplyStyle(style, target);
        menu.Show();
    }

    /// <summary>Applique le réglage « Afficher la pastille » à chaud. Depuis V7, les fenêtres ne sont plus
    /// détruites quand l'utilisateur les masque : elles sont cachées (Hide) pour pouvoir réapparaître pendant
    /// une dictée (retour visuel garanti même pastille masquée), voir <see cref="OnDictationVisual"/>.</summary>
    private void ApplyBadgeVisibility()
    {
        if (_config is null) return;
        if (_badges.Count == 0) { if (_config.ShowBadge) CreateBadges(); return; }
        ReconcileBadges(); // réapplique le réglage « écran(s) » même si des pastilles existent déjà
        if (_config.ShowBadge) foreach (var b in _badges) b.Show();
        else foreach (var b in _badges) b.Hide();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _theme?.Dispose();
        _controller?.Dispose();
        _hotkeyService?.Dispose();
        if (_displaySubscribed) SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        foreach (var b in _badges) b.Close();
        _tray?.Dispose();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
