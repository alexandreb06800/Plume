using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class ThemeServiceTests
{
    [Fact]
    public void IsWindowsDark_QuandValeurZero_RetourneVrai()
        => Assert.True(new ThemeService(() => 0).IsWindowsDark());

    [Fact]
    public void IsWindowsDark_QuandValeurUn_RetourneFaux()
        => Assert.False(new ThemeService(() => 1).IsWindowsDark());

    [Fact]
    public void IsWindowsDark_QuandCleAbsente_RetourneFaux()
        => Assert.False(new ThemeService(() => null).IsWindowsDark());

    [Fact]
    public void RefreshFromWindows_RechargeSeulementSiLeThemeChange()
    {
        int? valeur = 1; // Windows en clair
        using var svc = new ThemeService(() => valeur);
        int changements = 0;
        svc.Changed += _ => changements++;

        Assert.False(svc.RefreshFromWindows());
        Assert.Equal(0, changements);

        valeur = 0; // Windows passe en sombre
        Assert.True(svc.RefreshFromWindows());
        Assert.Equal(1, changements);
        Assert.True(svc.CurrentDark);
    }
}
