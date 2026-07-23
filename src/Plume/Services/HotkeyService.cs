using System.Runtime.InteropServices;
using System.Windows.Interop;
using Plume.Models;

namespace Plume.Services;

/// <summary>
/// Enregistre les raccourcis globaux via RegisterHotKey sur une fenêtre message-only :
/// la dictée (un seul raccourci global), l'assistant vocal, et Échap (annulation temporaire).
/// Rebind à chaud sans redémarrage.
/// </summary>
public class HotkeyService : IDisposable
{
    private const int WM_HOTKEY = 0x0312;
    private const int ESC_ID = 0xB0B1;
    private const int AGENT_ID = 0xB0B2;
    private const int DICTATION_ID = 0xB0B3;
    private const uint MOD_NOREPEAT = 0x4000;
    private static readonly IntPtr HWND_MESSAGE = new(-3);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private readonly HwndSource _source;
    private uint _dictationVk; // vk de la touche principale (maintien enfoncé)
    private uint _agentVk;

    public event Action? EscapePressed;
    /// <summary>Émis quand le raccourci de dictée est pressé : vk de la touche principale (pour le maintien).</summary>
    public event Action<uint>? DictationHotkeyPressed;
    /// <summary>Émis quand le raccourci de l'assistant vocal est pressé : vk de la touche principale.</summary>
    public event Action<uint>? AgentHotkeyPressed;

    public HotkeyService()
    {
        var parameters = new HwndSourceParameters("PlumeHotkeyWindow")
        {
            ParentWindow = HWND_MESSAGE,
            WindowStyle = 0
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            int id = wParam.ToInt32();
            if (id == ESC_ID) { EscapePressed?.Invoke(); handled = true; }
            else if (id == AGENT_ID) { AgentHotkeyPressed?.Invoke(_agentVk); handled = true; }
            else if (id == DICTATION_ID) { DictationHotkeyPressed?.Invoke(_dictationVk); handled = true; }
        }
        return IntPtr.Zero;
    }

    public void RegisterEscape()
    {
        // Échec silencieux assumé côté UX (Échap pris par une autre app : l'annulation passe par la pastille),
        // mais tracé en debug pour retrouver le cas dans un diagnostic « Échap ne marche pas ».
        if (!RegisterHotKey(_source.Handle, ESC_ID, MOD_NOREPEAT, 0x1B))
            System.Diagnostics.Debug.WriteLine("[Plume] RegisterHotKey(Échap) refusé : annulation par Échap indisponible.");
    }
    public void UnregisterEscape() => UnregisterHotKey(_source.Handle, ESC_ID);

    /// <summary>(Ré)enregistre le raccourci de dictée. Renvoie un message FR si invalide/pris, sinon null.</summary>
    public string? RegisterDictationHotkey(HotkeyConfig hk)
    {
        UnregisterHotKey(_source.Handle, DICTATION_ID);
        _dictationVk = 0;
        if (!HotkeyParser.TryParse(hk, out var mods, out var vk, out var err))
            return err ?? "Raccourci invalide.";
        if (!RegisterHotKey(_source.Handle, DICTATION_ID, mods | MOD_NOREPEAT, vk))
            return "Ce raccourci est déjà utilisé, choisis-en un autre.";
        _dictationVk = vk;
        return null;
    }

    public void UnregisterDictationHotkey()
    {
        UnregisterHotKey(_source.Handle, DICTATION_ID);
        _dictationVk = 0;
    }

    /// <summary>(Ré)enregistre le raccourci de l'assistant vocal. Renvoie un message FR ou null.</summary>
    public string? RegisterAgentHotkey(HotkeyConfig hk)
    {
        UnregisterHotKey(_source.Handle, AGENT_ID);
        _agentVk = 0;
        if (!HotkeyParser.TryParse(hk, out var mods, out var vk, out var err))
            return err ?? "Raccourci invalide.";
        if (!RegisterHotKey(_source.Handle, AGENT_ID, mods | MOD_NOREPEAT, vk))
            return "Ce raccourci est déjà utilisé, choisis-en un autre.";
        _agentVk = vk;
        return null;
    }

    public void UnregisterAgentHotkey()
    {
        UnregisterHotKey(_source.Handle, AGENT_ID);
        _agentVk = 0;
    }

    public void Dispose()
    {
        UnregisterHotKey(_source.Handle, ESC_ID);
        UnregisterHotKey(_source.Handle, AGENT_ID);
        UnregisterHotKey(_source.Handle, DICTATION_ID);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
