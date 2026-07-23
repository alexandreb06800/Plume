using Plume.Models;
using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class HotkeyParserTests
{
    [Fact]
    public void TryParse_AltEspace_OK()
    {
        var cfg = new HotkeyConfig { Modifiers = new() { "Alt" }, Key = "Space" };
        var ok = HotkeyParser.TryParse(cfg, out var mods, out var vk, out var err);

        Assert.True(ok);
        Assert.Null(err);
        Assert.Equal(HotkeyParser.MOD_ALT, mods);
        Assert.Equal(0x20u, vk); // VK_SPACE
    }

    [Fact]
    public void TryParse_CtrlAltEspace_CombineLesModificateurs()
    {
        var cfg = new HotkeyConfig { Modifiers = new() { "Ctrl", "Alt" }, Key = "Space" };
        var ok = HotkeyParser.TryParse(cfg, out var mods, out _, out _);

        Assert.True(ok);
        Assert.Equal(HotkeyParser.MOD_CONTROL | HotkeyParser.MOD_ALT, mods);
    }

    [Fact]
    public void TryParse_SansModificateur_Refuse()
    {
        var cfg = new HotkeyConfig { Modifiers = new(), Key = "Space" };
        var ok = HotkeyParser.TryParse(cfg, out _, out _, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
    }

    [Fact]
    public void TryParse_ToucheInconnue_Refuse()
    {
        var cfg = new HotkeyConfig { Modifiers = new() { "Alt" }, Key = "Pizza" };
        var ok = HotkeyParser.TryParse(cfg, out _, out _, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
    }

    [Fact]
    public void Describe_RenduLisibleFr()
    {
        var cfg = new HotkeyConfig { Modifiers = new() { "Ctrl", "Alt" }, Key = "Space" };
        Assert.Equal("Ctrl + Alt + Espace", HotkeyParser.Describe(cfg));
    }

    [Fact]
    public void TryParse_ModifiersNull_Refuse()
    {
        var cfg = new HotkeyConfig { Modifiers = null!, Key = "Space" };
        var ok = HotkeyParser.TryParse(cfg, out _, out _, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
    }

    [Fact]
    public void TryParse_ModificateurInconnu_Refuse()
    {
        var cfg = new HotkeyConfig { Modifiers = new() { "Hyper" }, Key = "Space" };
        var ok = HotkeyParser.TryParse(cfg, out _, out _, out var err);

        Assert.False(ok);
        Assert.Contains("Hyper", err);
    }

    [Fact]
    public void TryParse_SynonymesEtCasse_OK()
    {
        // « maj », « windows », « control » (toutes casses) sont des synonymes acceptés.
        var cfg = new HotkeyConfig { Modifiers = new() { "MAJ", "Windows", "control" }, Key = "F9" };
        var ok = HotkeyParser.TryParse(cfg, out var mods, out _, out _);

        Assert.True(ok);
        Assert.Equal(HotkeyParser.MOD_SHIFT | HotkeyParser.MOD_WIN | HotkeyParser.MOD_CONTROL, mods);
    }

    [Fact]
    public void TryParse_KeyNulle_Refuse()
    {
        var cfg = new HotkeyConfig { Modifiers = new() { "Alt" }, Key = null! };
        var ok = HotkeyParser.TryParse(cfg, out _, out _, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
    }

    [Fact]
    public void TryParse_KeyNone_Refuse()
    {
        var cfg = new HotkeyConfig { Modifiers = new() { "Alt" }, Key = "None" };
        var ok = HotkeyParser.TryParse(cfg, out _, out _, out var err);

        Assert.False(ok);
        Assert.NotNull(err);
    }

    [Fact]
    public void Describe_KeyNulle_PasDeCrash()
    {
        var cfg = new HotkeyConfig { Modifiers = new() { "Ctrl" }, Key = null! };
        Assert.Equal("Ctrl", HotkeyParser.Describe(cfg));
    }
}
