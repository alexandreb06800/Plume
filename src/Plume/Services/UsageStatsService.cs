namespace Plume.Services;

/// <summary>Logique pure des statistiques d'usage : comptage, temps gagné.</summary>
public static class UsageStatsService
{
    private const double WordsPerMinute = 40.0; // hypothèse de frappe

    /// <summary>Compte les mots (séparés par des blancs).</summary>
    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>Minutes gagnées estimées (frappe ~40 mots/min).</summary>
    public static double MinutesSaved(long wordCount) => wordCount / WordsPerMinute;

    /// <summary>Formate le temps gagné : « ~0 min », « ~10 min », « ~1 h », « ~1 h 01 », « ~4 h 10 ».</summary>
    public static string FormatTimeSaved(long wordCount)
    {
        var totalMin = (int)Math.Round(MinutesSaved(wordCount));
        if (totalMin < 60) return $"~{totalMin} min";
        int h = totalMin / 60, m = totalMin % 60;
        return m == 0 ? $"~{h} h" : $"~{h} h {m:D2}";
    }
}
