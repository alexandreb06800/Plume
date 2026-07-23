using System;
using System.Text;
using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class MarkdownHtmlTests
{
    // ---- ToHtml ----

    [Fact]
    public void ToHtml_TexteVide_RendVide()
    {
        Assert.Equal("", MarkdownHtml.ToHtml(""));
        Assert.Equal("", MarkdownHtml.ToHtml(null!));
    }

    [Fact]
    public void ToHtml_ParagrapheSimple_Div()
    {
        Assert.Equal("<div>Bonjour Olivier,</div>", MarkdownHtml.ToHtml("Bonjour Olivier,"));
    }

    [Fact]
    public void ToHtml_Gras_Balise()
    {
        Assert.Equal("<div>Un <b>comportement inattendu</b> ici</div>",
            MarkdownHtml.ToHtml("Un **comportement inattendu** ici"));
    }

    [Fact]
    public void ToHtml_Italique_Et_Code()
    {
        Assert.Equal("<div><i>ital</i> et <span style=\"font-family:Consolas,monospace\">x = 1</span></div>",
            MarkdownHtml.ToHtml("*ital* et `x = 1`"));
    }

    [Fact]
    public void ToHtml_EntitesEchappees()
    {
        Assert.Equal("<div>a &lt; b &amp; c &gt; d</div>", MarkdownHtml.ToHtml("a < b & c > d"));
    }

    [Fact]
    public void ToHtml_PucesConsecutives_GroupeesDansUnSeulUl()
    {
        Assert.Equal("<ul><li>un</li><li>deux</li></ul>", MarkdownHtml.ToHtml("- un\n- deux"));
    }

    [Fact]
    public void ToHtml_ListeNumerotee_ConserveLeDepart()
    {
        Assert.Equal("<ol start=\"3\"><li>trois</li><li>quatre</li></ol>",
            MarkdownHtml.ToHtml("3. trois\n4. quatre"));
    }

    [Fact]
    public void ToHtml_LigneVide_Br_EtFermeLaListe()
    {
        Assert.Equal("<ul><li>un</li></ul><br/><div>suite</div>", MarkdownHtml.ToHtml("- un\n\nsuite"));
    }

    [Fact]
    public void ToHtml_PuceEtoile_CommeTiret()
    {
        Assert.Equal("<ul><li>un</li></ul>", MarkdownHtml.ToHtml("* un"));
    }

    // ---- ToPlainText ----

    [Fact]
    public void ToPlainText_RetireLesMarqueurs()
    {
        Assert.Equal("Un comportement inattendu ici", MarkdownHtml.ToPlainText("Un **comportement inattendu** ici"));
    }

    [Fact]
    public void ToPlainText_ConserveLignesVides_PucesEtNumeros()
    {
        Assert.Equal("- un\r\n\r\n2. deux", MarkdownHtml.ToPlainText("* un\n\n2. **deux**"));
    }

    // ---- ToCfHtmlBytes ----

    [Fact]
    public void ToCfHtmlBytes_OffsetsCorrects_MemeAvecAccents()
    {
        // Les accents pèsent 2 octets en UTF-8 : vérifie que les offsets sont bien en octets.
        var bytes = MarkdownHtml.ToCfHtmlBytes("Bonne **journée** à bientôt");
        var full = Encoding.UTF8.GetString(bytes);

        int startFrag = ExtractOffset(full, "StartFragment:");
        int endFrag = ExtractOffset(full, "EndFragment:");
        int startHtml = ExtractOffset(full, "StartHTML:");
        int endHtml = ExtractOffset(full, "EndHTML:");

        Assert.Equal(bytes.Length, endHtml);
        Assert.Equal("<html>", Encoding.UTF8.GetString(bytes, startHtml, 6));
        var fragment = Encoding.UTF8.GetString(bytes, startFrag, endFrag - startFrag);
        Assert.Equal("<div>Bonne <b>journée</b> à bientôt</div>", fragment);
    }

    private static int ExtractOffset(string cfHtml, string key)
    {
        int i = cfHtml.IndexOf(key, StringComparison.Ordinal);
        Assert.True(i >= 0, $"clé absente : {key}");
        return int.Parse(cfHtml.Substring(i + key.Length, 10));
    }
}
