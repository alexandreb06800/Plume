using System.Text;

namespace Plume.Services;

/// <summary>Conversion du markdown léger (mêmes règles que LightMarkdown) vers HTML de presse-papiers
/// (format CF_HTML, pour coller gras et listes dans Outlook/Word) et vers texte brut (marqueurs retirés).
/// Pur et testable, aucune dépendance WPF. Ne lève jamais.</summary>
public static class MarkdownHtml
{
    /// <summary>Fragment HTML du texte : un &lt;div&gt; par ligne (pas de marge parasite au collage),
    /// &lt;br/&gt; pour les lignes vides, puces/numéros groupés en &lt;ul&gt;/&lt;ol&gt;, gras/italique/code
    /// inline, entités échappées.</summary>
    public static string ToHtml(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var sb = new StringBuilder();
        string openList = ""; // "", "ul" ou "ol"
        void CloseList() { if (openList.Length > 0) { sb.Append("</").Append(openList).Append('>'); openList = ""; } }

        foreach (var raw in SplitLines(text))
        {
            var line = raw.TrimStart();
            if (line.Length == 0) { CloseList(); sb.Append("<br/>"); continue; }

            if (IsBullet(line))
            {
                if (openList != "ul") { CloseList(); sb.Append("<ul>"); openList = "ul"; }
                sb.Append("<li>").Append(InlineHtml(line.Substring(2))).Append("</li>");
                continue;
            }

            if (TryNumbered(line, out int number, out string rest))
            {
                if (openList != "ol") { CloseList(); sb.Append("<ol start=\"").Append(number).Append("\">"); openList = "ol"; }
                sb.Append("<li>").Append(InlineHtml(rest)).Append("</li>");
                continue;
            }

            CloseList();
            sb.Append("<div>").Append(InlineHtml(line)).Append("</div>");
        }
        CloseList();
        return sb.ToString();
    }

    /// <summary>Texte brut pour le presse-papiers : marqueurs **, * et ` retirés ; puces rendues « - » ;
    /// numérotation conservée ; lignes vides préservées.</summary>
    public static string ToPlainText(string text)
    {
        if (string.IsNullOrEmpty(text)) return "";

        var lines = new List<string>();
        foreach (var raw in SplitLines(text))
        {
            var line = raw.TrimStart();
            if (line.Length == 0) { lines.Add(""); continue; }
            if (IsBullet(line)) { lines.Add("- " + StripInline(line.Substring(2))); continue; }
            if (TryNumbered(line, out int number, out string rest)) { lines.Add($"{number}. " + StripInline(rest)); continue; }
            lines.Add(StripInline(line));
        }
        return string.Join("\r\n", lines);
    }

    /// <summary>Enveloppe CF_HTML complète (en-tête + offsets en octets UTF-8), prête à poser dans le
    /// presse-papiers Windows au format DataFormats.Html.</summary>
    public static byte[] ToCfHtmlBytes(string text)
    {
        var fragment = ToHtml(text);
        const string pre = "<html><body><!--StartFragment-->";
        const string post = "<!--EndFragment--></body></html>";

        // Les nombres sont à largeur fixe (10 chiffres) : la longueur de l'en-tête ne dépend pas des valeurs.
        static string Header(int startHtml, int endHtml, int startFrag, int endFrag) =>
            $"Version:0.9\r\nStartHTML:{startHtml:D10}\r\nEndHTML:{endHtml:D10}\r\nStartFragment:{startFrag:D10}\r\nEndFragment:{endFrag:D10}\r\n";

        int headerLen = Encoding.UTF8.GetByteCount(Header(0, 0, 0, 0));
        int startHtml = headerLen;
        int startFragment = headerLen + Encoding.UTF8.GetByteCount(pre);
        int endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
        int endHtml = endFragment + Encoding.UTF8.GetByteCount(post);

        return Encoding.UTF8.GetBytes(Header(startHtml, endHtml, startFragment, endFragment) + pre + fragment + post);
    }

    // --- Détection de lignes : mêmes règles que LightMarkdown.Parse (garder les deux en phase) ---

    private static string[] SplitLines(string text)
        => text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');

    private static bool IsBullet(string line)
        => line.Length >= 2 && (line[0] == '-' || line[0] == '*') && line[1] == ' ';

    private static bool TryNumbered(string line, out int number, out string rest)
    {
        number = 0; rest = "";
        int dot = line.IndexOf('.');
        if (dot > 0 && dot + 1 < line.Length && line[dot + 1] == ' '
            && AllDigits(line, dot) && int.TryParse(line.Substring(0, dot), out number))
        {
            rest = line.Substring(dot + 2);
            return true;
        }
        return false;
    }

    private static bool AllDigits(string s, int end)
    {
        for (int k = 0; k < end; k++) if (!char.IsDigit(s[k])) return false;
        return end > 0;
    }

    // --- Rendu inline (réutilise le parseur existant) ---

    private static string InlineHtml(string line)
    {
        var sb = new StringBuilder();
        foreach (var r in LightMarkdown.ParseInline(line))
        {
            var t = Escape(r.Text);
            switch (r.Kind)
            {
                case InlineKind.Bold: sb.Append("<b>").Append(t).Append("</b>"); break;
                case InlineKind.Italic: sb.Append("<i>").Append(t).Append("</i>"); break;
                case InlineKind.Code: sb.Append("<span style=\"font-family:Consolas,monospace\">").Append(t).Append("</span>"); break;
                default: sb.Append(t); break;
            }
        }
        return sb.ToString();
    }

    private static string StripInline(string line)
    {
        var sb = new StringBuilder();
        foreach (var r in LightMarkdown.ParseInline(line)) sb.Append(r.Text);
        return sb.ToString();
    }

    private static string Escape(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
