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
}
