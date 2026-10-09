using System.Text.RegularExpressions;
using Plume.Models;

namespace Plume.Services;

/// <summary>Applique les règles de remplacement exactes (mot entier, insensible à la casse) au texte final.</summary>
public static class ReplacementService
{
    public static string Apply(string text, IReadOnlyList<ReplacementRule> rules)
    {
        if (string.IsNullOrEmpty(text) || rules is null) return text;
        foreach (var r in rules)
        {
            if (string.IsNullOrWhiteSpace(r.From)) continue;
            var pattern = $@"\b{Regex.Escape(r.From)}\b";
            // MatchEvaluator : évite l'interprétation des $ dans le texte de remplacement.
            text = Regex.Replace(text, pattern, _ => r.To ?? "", RegexOptions.IgnoreCase);
        }
        return text;
    }

    /// <summary>Retire les espaces et tabulations en fin de chaque ligne (certains modèles terminent les lignes
    /// de liste par des espaces, héritage des sauts de ligne markdown) et normalise les fins de ligne en LF.
    /// Fonction pure.</summary>
    public static string TrimLineEnds(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++) lines[i] = lines[i].TrimEnd(' ', '\t');
        return string.Join("\n", lines);
    }

    // Déclencheurs triés du plus long au plus court : « retour à la ligne » doit être absorbé
    // en entier (pas seulement son suffixe « à la ligne »), sinon le mot « retour » reste orphelin.
    private static readonly Regex LineBreakPattern = new(
        @"[ \t]*[,\.]?[ \t]*\b(nouveau paragraphe|retour à la ligne|à la ligne|nouvelle ligne)\b[ \t]*[,\.]?[ \t]*(\p{L})?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Transforme les commandes vocales de saut de ligne (« à la ligne », « nouveau paragraphe »…)
    /// en vrais retours à la ligne. Absorbe la ponctuation adjacente et met en majuscule la lettre
    /// qui suit le saut, pour une phrase proprement close. Fonction pure (aucun état, aucune I/O).
    /// </summary>
    public static string ApplyLineBreaks(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return LineBreakPattern.Replace(text, m =>
        {
            var estParagraphe = string.Equals(m.Groups[1].Value, "nouveau paragraphe", StringComparison.OrdinalIgnoreCase);
            var saut = estParagraphe ? "\n\n" : "\n";
            var lettreSuivante = m.Groups[2].Value;
            var suite = lettreSuivante.Length > 0 ? lettreSuivante.ToUpperInvariant() : "";
            return "." + saut + suite;
        });
    }
}
