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
    // Avant la commande : blancs y compris retours à la ligne (\s) — le nettoyage peut avoir placé la commande
    // en tête de ligne (« retour.\nÀ la ligne, je… ») ; sans ça, le saut s'ajoutait au sien (« retour.\n.\nJe »).
    private static readonly Regex LineBreakPattern = new(
        @"\s*([,\.]?)\s*\b(nouveau paragraphe|retour à la ligne|à la ligne|nouvelle ligne)\b[ \t]*[,\.]?[ \t]*(\p{L})?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Transforme les commandes vocales de saut de ligne (« à la ligne », « nouveau paragraphe »…)
    /// en vrais retours à la ligne. Absorbe la ponctuation et les blancs adjacents, clôt la phrase précédente
    /// par un point s'il n'y a pas déjà une ponctuation finale (« ? », « ! »…), et met en majuscule la lettre
    /// qui suit le saut. Fonction pure (aucun état, aucune I/O).
    /// </summary>
    public static string ApplyLineBreaks(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return LineBreakPattern.Replace(text, m =>
        {
            var estParagraphe = string.Equals(m.Groups[2].Value, "nouveau paragraphe", StringComparison.OrdinalIgnoreCase);
            var saut = estParagraphe ? "\n\n" : "\n";
            var lettreSuivante = m.Groups[3].Value;
            var suite = lettreSuivante.Length > 0 ? lettreSuivante.ToUpperInvariant() : "";

            // Ponctuation de clôture : rien en tout début de texte ; le point s'il a été absorbé par le motif ;
            // rien si la phrase précédente finit déjà par une ponctuation finale ; sinon un point.
            var avant = text[..m.Index].TrimEnd();
            string cloture;
            if (avant.Length == 0) cloture = "";
            else if (m.Groups[1].Value == ".") cloture = ".";
            else cloture = ".?!…:".Contains(avant[^1]) ? "" : ".";
            return cloture + saut + suite;
        });
    }
}
