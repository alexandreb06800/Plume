using System.Runtime.InteropServices;
using System.Windows.Threading;
using Plume.Models;

namespace Plume.Services;

/// <summary>Insère du texte au curseur via presse-papiers + Ctrl+V, puis restaure le presse-papiers.</summary>
public class InsertionService
{
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion U; }

    // L'union doit avoir la taille du plus gros membre (MOUSEINPUT) sinon
    // sizeof(INPUT) est faux et SendInput rejette l'appel (retourne 0).
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT
    {
        public uint uMsg; public ushort wParamL; public ushort wParamH;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] private static extern uint GetCurrentThreadId();

    /// <summary>Colle `text` au curseur en préservant le presse-papiers courant. Retourne <c>true</c>
    /// si le collage a été initié (voir <see cref="PasteAndRestore"/>), <c>false</c> sinon
    /// (ex. presse-papiers verrouillé) — dans ce cas rien n'a été modifié.</summary>
    public bool Paste(string text, IntPtr targetWindow)
    {
        if (string.IsNullOrEmpty(text)) return false;
        // Sauvegarde du presse-papiers courant (texte, image ou fichiers), puis collage en le restaurant ensuite.
        var previous = ClipboardSnapshot.Capture();
        return PasteAndRestore(text, targetWindow, previous);
    }

    /// <summary>Colle `text` au curseur (presse-papiers + Ctrl+V), puis restaure le presse-papiers vers
    /// `restoreTo` (ou le vide si null). Utilisé par la reformulation, qui doit restaurer le presse-papiers
    /// INITIAL (et non la sélection capturée juste avant).
    /// Retourne <c>true</c> si le collage a été initié, <c>false</c> si la mise en presse-papiers a échoué
    /// (presse-papiers verrouillé) — dans ce cas le presse-papiers n'est pas modifié.</summary>
    // Restauration en attente (timer de 400 ms pas encore écoulé). Tout vit sur le thread UI
    // (appels du contrôleur + Tick du DispatcherTimer) : aucun verrou nécessaire.
    private DispatcherTimer? _restoreTimer;
    private ClipboardSnapshot? _pendingRestore;

    public bool PasteAndRestore(string text, IntPtr targetWindow, ClipboardSnapshot? restoreTo)
    {
        if (string.IsNullOrEmpty(text)) return false;

        // Deux opérations enchaînées en < 400 ms : la restauration précédente n'a pas encore eu lieu, donc
        // l'instantané `restoreTo` capturé par l'opération courante contient encore le TEXTE INSÉRÉ par la
        // précédente — pas le presse-papiers de l'utilisateur. On reprend l'instantané en attente (le vrai)
        // et on annule l'ancien timer, qui aurait sinon écrasé le collage en cours à mi-chemin.
        var inherited = _pendingRestore;
        if (inherited is not null)
        {
            _restoreTimer?.Stop();
            _restoreTimer = null;
            _pendingRestore = null;
            restoreTo = inherited;
        }

        // Marqué comme transitoire : la dictée n'a pas sa place dans l'historique du presse-papiers (Win+V)
        // ni dans le presse-papiers infonuagique, même si elle n'y reste que le temps du Ctrl+V.
        try { ClipboardSnapshot.SetTextExcludedFromHistory(text); }
        catch
        {
            // Presse-papiers verrouillé : rien n'a été modifié. Ne pas perdre une restauration héritée —
            // le presse-papiers contient encore le texte de l'opération précédente, à restaurer quand même.
            if (inherited is not null) ArmRestore(inherited);
            return false;
        }

        FocusWindow(targetWindow);
        SendCtrlV();

        // Restauration différée pour laisser l'app cible consommer le collage.
        ArmRestore(restoreTo ?? ClipboardSnapshot.Empty());
        return true;
    }

    /// <summary>Programme la restauration du presse-papiers dans 400 ms. Une insertion plus récente
    /// peut la reprendre à son compte entre-temps (voir `_pendingRestore` dans PasteAndRestore).</summary>
    private void ArmRestore(ClipboardSnapshot snapshot)
    {
        _pendingRestore = snapshot;
        var timer = _restoreTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_restoreTimer != timer) return; // une insertion plus récente a repris la restauration à son compte
            _restoreTimer = null;
            var pending = _pendingRestore;
            _pendingRestore = null;
            pending?.Restore();
        };
        timer.Start();
    }

    /// <summary>Redonne le focus à la fenêtre cible (AttachThreadInput lève la restriction SetForegroundWindow).
    /// Utilisé avant le collage, et avant une capture de sélection (Ctrl+C synthétique) déclenchée depuis
    /// une fenêtre qui n'a pas le focus (ex. menu de la pastille). Ne lève jamais.</summary>
    public void FocusWindow(IntPtr targetWindow)
    {
        if (targetWindow == IntPtr.Zero) return;
        try
        {
            if (GetForegroundWindow() == targetWindow) return; // déjà au premier plan

            uint targetThread = GetWindowThreadProcessId(targetWindow, out _);
            uint currentThread = GetCurrentThreadId();
            bool attached = false;
            try
            {
                if (targetThread != currentThread)
                    attached = AttachThreadInput(currentThread, targetThread, true);
                SetForegroundWindow(targetWindow);
            }
            finally
            {
                if (attached) AttachThreadInput(currentThread, targetThread, false);
            }
        }
        catch { /* jamais de crash sur la restauration du focus */ }
    }

    private static uint SendCtrlV()
    {
        var inputs = new INPUT[]
        {
            Key(VK_CONTROL, false),
            Key(VK_V, false),
            Key(VK_V, true),
            Key(VK_CONTROL, true),
        };
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT Key(ushort vk, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? KEYEVENTF_KEYUP : 0 } }
    };
}
