using System.IO;
using Microsoft.Win32;

namespace Plume.Services;

/// <summary>Gère l'entrée « Démarrer avec Windows » dans HKCU...\Run.</summary>
public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Plume";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
        return key?.GetValue(ValueName) is not null;
    }

    public static void SetEnabled(bool enabled, string exePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)
                        ?? Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled) key.SetValue(ValueName, $"\"{ResolveRunPath(exePath)}\"");
        else key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    /// <summary>Chemin à inscrire dans la clé Run. L'exe installé (%LOCALAPPDATA%\Plume\Plume.exe) est
    /// préféré quand l'instance courante tourne depuis OneDrive (dev/dossier de build) : une entrée Run
    /// pointant dans OneDrive peut être retirée par la synchro/sécurité — le démarrage auto se perdait.</summary>
    public static string ResolveRunPath(string? processPath)
    {
        var installed = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Plume", "Plume.exe");
        return ResolveRunPath(processPath, installed, File.Exists);
    }

    /// <summary>Variante testable : chemin installé et test d'existence fournis.</summary>
    public static string ResolveRunPath(string? processPath, string installedExe, Func<string, bool> installedExists)
    {
        bool processUtilisable = !string.IsNullOrEmpty(processPath) && !IsRunningFromOneDrive(processPath);
        if (processUtilisable) return processPath!;
        return installedExists(installedExe) ? installedExe : (processPath ?? installedExe);
    }

    /// <summary>
    /// Vrai si l'exe est lancé depuis un dossier OneDrive. Dans ce cas le
    /// « Démarrer avec Windows » est peu fiable : au logon le fichier peut être
    /// déshydraté (en ligne uniquement) ou OneDrive pas encore prêt, et l'entrée
    /// Run peut être retirée par la synchro/sécurité. Plume doit tourner hors OneDrive.
    /// </summary>
    public static bool IsRunningFromOneDrive() => IsRunningFromOneDrive(Environment.ProcessPath);

    /// <summary>Variante testable : on fournit le chemin de l'exe.</summary>
    public static bool IsRunningFromOneDrive(string? exe)
    {
        if (string.IsNullOrEmpty(exe)) return false;

        foreach (var v in new[] { "OneDrive", "OneDriveCommercial", "OneDriveConsumer" })
        {
            var root = Environment.GetEnvironmentVariable(v);
            if (!string.IsNullOrEmpty(root)
                && exe.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        // Repli : un segment « OneDrive » dans le chemin (dossiers renommés « OneDrive - Société »).
        return exe.Contains(@"\OneDrive", StringComparison.OrdinalIgnoreCase);
    }
}
