using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class UsageStatsServiceTests
{
    [Theory]
    [InlineData("", 0)]
    [InlineData("   ", 0)]
    [InlineData("bonjour", 1)]
    [InlineData("bonjour le monde", 3)]
    [InlineData("  espaces   multiples\tici ", 3)]
    public void CountWords_CompteLesMots(string texte, int attendu)
        => Assert.Equal(attendu, UsageStatsService.CountWords(texte));

    [Fact]
    public void CountWords_Null_RetourneZero()
        => Assert.Equal(0, UsageStatsService.CountWords(null));

    [Theory]
    [InlineData(0, "~0 min")]
    [InlineData(400, "~10 min")]    // 400/40 = 10 min
    [InlineData(2400, "~1 h")]      // 60 min
    [InlineData(2440, "~1 h 01")]   // 61 min
    [InlineData(10000, "~4 h 10")]  // 250 min
    public void FormatTimeSaved_FormateLeTemps(long mots, string attendu)
        => Assert.Equal(attendu, UsageStatsService.FormatTimeSaved(mots));
}
