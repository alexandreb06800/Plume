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

    [Fact]
    public void BuildInviteUri_ContientObjetEtCorpsSansPieceJointe()
    {
        var uri = UsageStatsService.BuildInviteUri();
        Assert.StartsWith("mailto:?subject=", uri);
        Assert.Contains(System.Uri.EscapeDataString(UsageStatsService.InviteSubject), uri);
        // Le repli mailto ne joint rien : le corps doit demander l'installateur, pas dire « joint ».
        Assert.Contains(System.Uri.EscapeDataString("demande-moi"), uri);
    }

    [Fact]
    public void BuildInviteBody_AdapteLeParagrapheInstallation()
    {
        var avecPj = UsageStatsService.BuildInviteBody(attachmentIncluded: true);
        var sansPj = UsageStatsService.BuildInviteBody(attachmentIncluded: false);
        Assert.Contains("joint à cet email", avecPj);
        Assert.DoesNotContain("demande-moi", avecPj);
        Assert.Contains("demande-moi", sansPj);
        Assert.DoesNotContain("joint à cet email", sansPj);
        // Les deux variantes présentent l'app et préviennent de l'avertissement SmartScreen.
        Assert.Contains("Ctrl+Alt+A", avecPj);
        Assert.Contains("Windows a protégé votre ordinateur", sansPj);
    }
}
