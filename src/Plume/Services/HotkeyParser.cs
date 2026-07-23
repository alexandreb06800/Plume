using System.Windows.Input;
using Plume.Models;

namespace Plume.Services;

/// <summary>Valide une combinaison et la convertit en flags/code Win32 pour RegisterHotKey.</summary>
public static class HotkeyParser
{
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    public static bool TryParse(HotkeyConfig cfg, out uint modifiers, out uint virtualKey, out string? error)
    {
        modifiers = 0;
        virtualKey = 0;
        error = null;

        if (cfg.Modifiers is null || cfg.Modifiers.Count == 0)
        {
            error = "Le raccourci doit contenir au moins un modificateur (Ctrl, Alt, Maj ou Win).";
            return false;
        }

        foreach (var m in cfg.Modifiers)
        {
            switch (m.Trim().ToLowerInvariant())
            {
                case "ctrl": case "control": modifiers |= MOD_CONTROL; break;
                case "alt": modifiers |= MOD_ALT; break;
                case "shift": case "maj": modifiers |= MOD_SHIFT; break;
                case "win": case "windows": modifiers |= MOD_WIN; break;
                default:
                    error = $"Modificateur inconnu : « {m} ».";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(cfg.Key) || !Enum.TryParse<Key>(cfg.Key, ignoreCase: true, out var key)
            || key == Key.None)
        {
            error = "Touche du raccourci invalide.";
            return false;
        }

        var vk = KeyInterop.VirtualKeyFromKey(key);
        if (vk == 0)
        {
            error = "Touche du raccourci invalide.";
            return false;
        }

        virtualKey = (uint)vk;
        return true;
    }

    /// <summary>Rendu lisible en français, ex. « Ctrl + Alt + Espace ».</summary>
    public static string Describe(HotkeyConfig cfg)
    {
        var parts = new List<string>();
        foreach (var m in cfg.Modifiers ?? new())
        {
            parts.Add(m.Trim().ToLowerInvariant() switch
            {
                "ctrl" or "control" => "Ctrl",
                "alt" => "Alt",
                "shift" or "maj" => "Maj",
                "win" or "windows" => "Win",
                _ => m
            });
        }
        parts.Add(cfg.Key?.Trim().ToLowerInvariant() == "space" ? "Espace" : cfg.Key ?? "");
        return string.Join(" + ", parts.Where(p => !string.IsNullOrEmpty(p)));
    }
}
