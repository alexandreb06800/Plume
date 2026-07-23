using System.Runtime.InteropServices;
using Plume.Models;

namespace Plume.Services;

/// <summary>Texte capturé depuis la sélection active + état du presse-papiers à restaurer.</summary>
public sealed class CapturedSelection
{
    public string? Text { get; init; } // null/vide = aucune sélection
    public ClipboardSnapshot Original { get; init; } = ClipboardSnapshot.Empty(); // presse-papiers avant capture
}

/// <summary>
/// Capture la sélection courante via Ctrl+C, en distinguant « aucune sélection » d'un vrai contenu
/// grâce à un sentinel : on écrit un marqueur dans le presse-papiers, on envoie Ctrl+C, puis on relit.
/// Si le presse-papiers vaut encore le sentinel, Ctrl+C n'a rien copié → aucune sélection.
/// </summary>
public class SelectionService
{
    private const string Sentinel = "PLUME_SENTINEL";
    private const int PollMs = 30;
    private const int TimeoutMs = 600;

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_C = 0x43;

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public uint type; public InputUnion U; }
    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    private struct HARDWAREINPUT { public uint uMsg; public ushort wParamL; public ushort wParamH; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    /// <summary>Décide du résultat de la capture : null si le presse-papiers n'a pas changé
    /// (= aucune sélection), sinon le texte copié. Pur et testable.</summary>
    public static string? InterpretCapture(string sentinel, string? clipboardAfter)
    {
        if (string.IsNullOrEmpty(clipboardAfter)) return null;
        if (clipboardAfter == sentinel) return null;
        return clipboardAfter;
    }

    /// <summary>Capture la sélection de la fenêtre au premier plan. Ne lève jamais. Ne restaure pas
    /// le presse-papiers (le caller décide : RestoreClipboard sur échec, ou collage du résultat sur succès).
    /// Async non-bloquant : `await Task.Delay` entre les lectures → le thread UI ne gèle jamais (l'overlay
    /// reste fluide). À appeler SUR le thread UI (le presse-papiers WPF exige le thread STA).</summary>
    public async Task<CapturedSelection> CaptureSelectionAsync()
    {
        var original = ClipboardSnapshot.Capture();

        try { ClipboardSnapshot.SetTextExcludedFromHistory(Sentinel); }
        catch { return new CapturedSelection { Text = null, Original = original }; }

        SendCtrlC();

        string? after = Sentinel;
        int waited = 0;
        while (waited < TimeoutMs)
        {
            await Task.Delay(PollMs); // non-bloquant : rend la main au message loop UI
            waited += PollMs;
            try { after = Clipboard.ContainsText() ? Clipboard.GetText() : null; } catch { after = null; }
            if (after != Sentinel) break; // l'app a écrit la sélection
        }

        return new CapturedSelection
        {
            Text = InterpretCapture(Sentinel, after),
            Original = original
        };
    }

    /// <summary>Restaure le presse-papiers tel qu'il était avant la capture (texte, image, fichiers ou vide).
    /// Ne lève jamais.</summary>
    public void RestoreClipboard(CapturedSelection sel) => sel.Original.Restore();

    private static void SendCtrlC()
    {
        var inputs = new INPUT[]
        {
            Key(VK_CONTROL, false),
            Key(VK_C, false),
            Key(VK_C, true),
            Key(VK_CONTROL, true),
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT Key(ushort vk, bool up) => new()
    {
        type = INPUT_KEYBOARD,
        U = new InputUnion { ki = new KEYBDINPUT { wVk = vk, dwFlags = up ? KEYEVENTF_KEYUP : 0 } }
    };
}
