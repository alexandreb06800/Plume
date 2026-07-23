using System.Text;

namespace Plume.Services;

/// <summary>Type de fragment inline issu du markdown léger.</summary>
public enum InlineKind { Normal, Bold, Italic, Code }

/// <summary>Fragment de texte inline (plat, sans imbrication).</summary>
public sealed record InlineRun(InlineKind Kind, string Text);

/// <summary>Type de bloc (une ligne).</summary>
public enum BlockKind { Paragraph, Bullet, Numbered }

/// <summary>Bloc de markdown léger : un paragraphe, une puce ou un élément numéroté.</summary>
public sealed record MarkdownBlock(BlockKind Kind, IReadOnlyList<InlineRun> Inlines, int Number = 0);

/// <summary>Parseur markdown léger, pur et testable (aucune dépendance WPF). Gère le gras (**),
/// l'italique (*), le code inline (`), les puces (- / *) et les listes numérotées (1.). Robuste :
/// un marqueur non fermé ou vide reste du texte littéral (jamais d'exception).</summary>
public static class LightMarkdown
{
    /// <summary>Découpe le texte en blocs (un par ligne non vide), dans l'ordre.</summary>
    public static IReadOnlyList<MarkdownBlock> Parse(string text)
    {
        var blocks = new List<MarkdownBlock>();
        if (string.IsNullOrEmpty(text)) return blocks;

        var lines = text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
        foreach (var raw in lines)
        {
            var line = raw.TrimStart();
            if (line.Length == 0) continue; // ligne vide = séparateur

            // Puce : '-' ou '*' suivi d'un espace.
            if (line.Length >= 2 && (line[0] == '-' || line[0] == '*') && line[1] == ' ')
            {
                blocks.Add(new MarkdownBlock(BlockKind.Bullet, ParseInline(line.Substring(2))));
                continue;
            }

            // Numérotée : un ou plusieurs chiffres, un point, un espace.
            int dot = line.IndexOf('.');
            if (dot > 0 && dot + 1 < line.Length && line[dot + 1] == ' '
                && AllDigits(line, dot) && int.TryParse(line.Substring(0, dot), out int number))
            {
                blocks.Add(new MarkdownBlock(BlockKind.Numbered, ParseInline(line.Substring(dot + 2)), number));
                continue;
            }

            blocks.Add(new MarkdownBlock(BlockKind.Paragraph, ParseInline(line)));
        }
        return blocks;
    }

    /// <summary>Découpe une ligne en fragments inline plats (Normal / Bold / Italic / Code).</summary>
    public static IReadOnlyList<InlineRun> ParseInline(string line)
    {
        var runs = new List<InlineRun>();
        if (string.IsNullOrEmpty(line)) return runs;

        var normal = new StringBuilder();
        void FlushNormal()
        {
            if (normal.Length > 0) { runs.Add(new InlineRun(InlineKind.Normal, normal.ToString())); normal.Clear(); }
        }

        int i = 0;
        while (i < line.Length)
        {
            char c = line[i];

            // Code `...` (contenu non vide, fermé).
            if (c == '`')
            {
                int end = line.IndexOf('`', i + 1);
                if (end > i + 1)
                {
                    FlushNormal();
                    runs.Add(new InlineRun(InlineKind.Code, line.Substring(i + 1, end - i - 1)));
                    i = end + 1;
                    continue;
                }
            }
            // Gras **...** (testé avant l'italique ; contenu non vide).
            else if (c == '*' && i + 1 < line.Length && line[i + 1] == '*')
            {
                int end = line.IndexOf("**", i + 2, System.StringComparison.Ordinal);
                if (end > i + 2)
                {
                    FlushNormal();
                    runs.Add(new InlineRun(InlineKind.Bold, line.Substring(i + 2, end - i - 2)));
                    i = end + 2;
                    continue;
                }
            }
            // Italique *...* (contenu non vide).
            else if (c == '*')
            {
                int end = line.IndexOf('*', i + 1);
                if (end > i + 1)
                {
                    FlushNormal();
                    runs.Add(new InlineRun(InlineKind.Italic, line.Substring(i + 1, end - i - 1)));
                    i = end + 1;
                    continue;
                }
            }

            normal.Append(c);
            i++;
        }
        FlushNormal();
        return runs;
    }

    private static bool AllDigits(string s, int end)
    {
        for (int k = 0; k < end; k++) if (!char.IsDigit(s[k])) return false;
        return end > 0;
    }
}
