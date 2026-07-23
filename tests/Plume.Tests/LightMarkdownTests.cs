using System.Linq;
using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class LightMarkdownTests
{
    // ---- ParseInline ----

    [Fact]
    public void ParseInline_Gras()
    {
        var runs = LightMarkdown.ParseInline("**gras**");
        var r = Assert.Single(runs);
        Assert.Equal(InlineKind.Bold, r.Kind);
        Assert.Equal("gras", r.Text);
    }

    [Fact]
    public void ParseInline_Italique()
    {
        var runs = LightMarkdown.ParseInline("*ital*");
        var r = Assert.Single(runs);
        Assert.Equal(InlineKind.Italic, r.Kind);
        Assert.Equal("ital", r.Text);
    }

    [Fact]
    public void ParseInline_Code()
    {
        var runs = LightMarkdown.ParseInline("`x = 1`");
        var r = Assert.Single(runs);
        Assert.Equal(InlineKind.Code, r.Kind);
        Assert.Equal("x = 1", r.Text);
    }

    [Fact]
    public void ParseInline_Melange_PreserveOrdre()
    {
        var runs = LightMarkdown.ParseInline("a **b** c");
        Assert.Equal(3, runs.Count);
        Assert.Equal((InlineKind.Normal, "a "), (runs[0].Kind, runs[0].Text));
        Assert.Equal((InlineKind.Bold, "b"), (runs[1].Kind, runs[1].Text));
        Assert.Equal((InlineKind.Normal, " c"), (runs[2].Kind, runs[2].Text));
    }

    [Fact]
    public void ParseInline_GrasPrioritaireSurItalique()
    {
        var runs = LightMarkdown.ParseInline("**x**");
        var r = Assert.Single(runs);
        Assert.Equal(InlineKind.Bold, r.Kind); // pas deux runs Italic
    }

    [Fact]
    public void ParseInline_MarqueurNonFerme_ResteLitteral()
    {
        var runs = LightMarkdown.ParseInline("a ** b");
        var r = Assert.Single(runs);
        Assert.Equal(InlineKind.Normal, r.Kind);
        Assert.Equal("a ** b", r.Text);
    }

    [Fact]
    public void ParseInline_MarqueurVide_ResteLitteral()
    {
        var runs = LightMarkdown.ParseInline("****");
        var r = Assert.Single(runs);
        Assert.Equal(InlineKind.Normal, r.Kind);
        Assert.Equal("****", r.Text);
    }

    [Fact]
    public void ParseInline_TexteSimple()
    {
        var runs = LightMarkdown.ParseInline("bonjour");
        var r = Assert.Single(runs);
        Assert.Equal(InlineKind.Normal, r.Kind);
    }

    [Fact]
    public void ParseInline_Vide_ListeVide()
    {
        Assert.Empty(LightMarkdown.ParseInline(""));
    }

    // ---- Parse (niveau bloc) ----

    [Theory]
    [InlineData("- item")]
    [InlineData("* item")]
    public void Parse_Puce(string line)
    {
        var b = Assert.Single(LightMarkdown.Parse(line));
        Assert.Equal(BlockKind.Bullet, b.Kind);
        var r = Assert.Single(b.Inlines);
        Assert.Equal("item", r.Text);
    }

    [Fact]
    public void Parse_Numerotee_ConserveNumero()
    {
        var b = Assert.Single(LightMarkdown.Parse("3. troisieme"));
        Assert.Equal(BlockKind.Numbered, b.Kind);
        Assert.Equal(3, b.Number);
        Assert.Equal("troisieme", Assert.Single(b.Inlines).Text);
    }

    [Fact]
    public void Parse_Paragraphe()
    {
        var b = Assert.Single(LightMarkdown.Parse("juste du texte"));
        Assert.Equal(BlockKind.Paragraph, b.Kind);
    }

    [Fact]
    public void Parse_LignesVides_Ignorees()
    {
        var blocks = LightMarkdown.Parse("a\n\n\nb");
        Assert.Equal(2, blocks.Count);
        Assert.All(blocks, b => Assert.Equal(BlockKind.Paragraph, b.Kind));
    }

    [Fact]
    public void Parse_MultiLignes_OrdrePreserve()
    {
        var blocks = LightMarkdown.Parse("Intro :\n- un\n- deux");
        Assert.Equal(3, blocks.Count);
        Assert.Equal(BlockKind.Paragraph, blocks[0].Kind);
        Assert.Equal(BlockKind.Bullet, blocks[1].Kind);
        Assert.Equal(BlockKind.Bullet, blocks[2].Kind);
        Assert.Equal("un", blocks[1].Inlines.Single().Text);
    }

    [Fact]
    public void Parse_PuceAvecGras()
    {
        var b = Assert.Single(LightMarkdown.Parse("- **gras** ici"));
        Assert.Equal(BlockKind.Bullet, b.Kind);
        Assert.Contains(b.Inlines, r => r.Kind == InlineKind.Bold && r.Text == "gras");
    }

    [Fact]
    public void Parse_Vide_ListeVide()
    {
        Assert.Empty(LightMarkdown.Parse(""));
    }
}
