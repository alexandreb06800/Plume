using System.IO;

namespace Plume.Services;

/// <summary>Journal de diagnostic opt-in (V8-H2) : quelques lignes par dictée (horodatage, étape,
/// durées, erreur éventuelle) pour permettre d'aider un collègue à distance. Jamais le texte dicté,
/// jamais les clés API. Borné à ~500 Ko avec rotation simple ; écriture hors chemin de latence.</summary>
public class DiagLogService
{
    private const long MaxBytes = 500_000;
    private readonly string _dir;

    public DiagLogService() : this(DefaultDir()) { }

    /// <summary>Constructeur testable avec dossier explicite.</summary>
    public DiagLogService(string dir) => _dir = dir;

    private static string DefaultDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Plume");

    public string LogPath => Path.Combine(_dir, "diag.log");
    private string OldLogPath => Path.Combine(_dir, "diag.old.log");

    /// <summary>No-op si désactivé (aucune écriture, aucun formatage) ; sinon écrit en tâche de fond
    /// (le retour n'est pas destiné à être attendu par l'appelant, qui reste hors du chemin de latence).</summary>
    public Task Log(bool enabled, string step, string? detail = null) =>
        enabled ? Task.Run(() => WriteNow(step, detail)) : Task.CompletedTask;

    /// <summary>Comme <see cref="Log"/>, mais écrit dans le thread appelant avant de rendre la main : pour un
    /// plantage fatal, où le processus va s'arrêter et où une écriture en tâche de fond n'aurait pas le temps
    /// de se faire. No-op si désactivé.</summary>
    public void LogNow(bool enabled, string step, string? detail = null)
    {
        if (enabled) WriteNow(step, detail);
    }

    /// <summary>Écrit immédiatement (synchrone). Ne lève jamais.</summary>
    public void WriteNow(string step, string? detail = null)
    {
        try
        {
            Directory.CreateDirectory(_dir);
            RotateIfNeeded();
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {step}" + (string.IsNullOrEmpty(detail) ? "" : $" — {detail}");
            File.AppendAllText(LogPath, line + Environment.NewLine);
        }
        catch { /* le journal de diagnostic ne doit jamais perturber la dictée */ }
    }

    private void RotateIfNeeded()
    {
        if (!File.Exists(LogPath)) return;
        if (new FileInfo(LogPath).Length <= MaxBytes) return;
        File.Move(LogPath, OldLogPath, overwrite: true);
    }
}
