using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Plume.Services;

/// <summary>Agrégat d'un jour : d = dictées, w = mots.</summary>
public class DayUsage
{
    [JsonPropertyName("d")] public long D { get; set; }
    [JsonPropertyName("w")] public long W { get; set; }
}

/// <summary>Agrégats journaliers d'usage. Fichier séparé de config.json, JAMAIS lu au démarrage
/// (écrit après chaque dictée hors chemin de latence, lu à l'ouverture des Réglages).</summary>
public static class DailyUsageService
{
    private const string DateFormat = "yyyy-MM-dd";
    private const int MaxDays = 400; // ~13 mois, fichier borné

    private static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Plume", "usage-daily.json");

    // --- Logique pure (testable) ---

    public static void Record(Dictionary<string, DayUsage> days, DateTime day, int words)
    {
        var key = day.ToString(DateFormat, CultureInfo.InvariantCulture);
        if (!days.TryGetValue(key, out var u)) days[key] = u = new DayUsage();
        u.D++;
        u.W += words;
    }

    public static void Prune(Dictionary<string, DayUsage> days, DateTime today)
    {
        var limit = today.AddDays(-MaxDays);
        foreach (var key in days.Keys.ToList())
            if (!TryParse(key, out var d) || d < limit) days.Remove(key);
    }

    public static (long dictations, long words) Sum(Dictionary<string, DayUsage> days, DateTime from, DateTime to)
    {
        long dict = 0, words = 0;
        foreach (var (key, u) in days)
            if (TryParse(key, out var d) && d >= from.Date && d <= to.Date) { dict += u.D; words += u.W; }
        return (dict, words);
    }

    private static bool TryParse(string key, out DateTime d) =>
        DateTime.TryParseExact(key, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out d);

    private static readonly object RecordLock = new();

    /// <summary>Lit, incrémente, purge et réécrit l'agrégat du jour, sous verrou : appelable depuis une tâche
    /// d'arrière-plan sans perdre de comptage si deux dictées se suivent de près. Ne lève jamais.</summary>
    public static void RecordDictation(DateTime today, int words)
    {
        lock (RecordLock)
        {
            var days = Load();
            Record(days, today, words);
            Prune(days, today);
            Save(days);
        }
    }

    // --- Stockage (jamais de crash : fichier absent/corrompu => dico vide) ---

    public static Dictionary<string, DayUsage> Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return new();
            return JsonSerializer.Deserialize<Dictionary<string, DayUsage>>(File.ReadAllText(FilePath)) ?? new();
        }
        catch { return new(); }
    }

    public static void Save(Dictionary<string, DayUsage> days)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            // Écriture atomique : ce fichier est réécrit après CHAQUE dictée — un kill en pleine écriture
            // le corromprait et Load() repartirait de zéro (perte silencieuse de l'historique).
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(days));
            File.Move(tmp, FilePath, overwrite: true);
        }
        catch { /* disque plein / verrou : on n'écrase pas, on réessaiera à la prochaine dictée */ }
    }
}
