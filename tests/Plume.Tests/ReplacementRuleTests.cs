using Plume.Models;
using Xunit;

namespace Plume.Tests;

public class ReplacementRuleTests
{
    [Fact]
    public void ToString_MonoLigne_Inchange()
    {
        var r = new ReplacementRule { From = "emevem", To = "exemple.fr" };
        Assert.Equal("emevem → exemple.fr", r.ToString());
    }

    [Fact]
    public void ToString_MultiLigne_AfficheAvecFlecheRetour()
    {
        var r = new ReplacementRule { From = "signature", To = "Cordialement,\nPrénom\nACME" };
        Assert.Equal("signature → Cordialement, ↵ Prénom ↵ ACME", r.ToString());
    }

    [Fact]
    public void ToString_RetoursCRLF_Normalises()
    {
        var r = new ReplacementRule { From = "x", To = "a\r\nb" };
        Assert.Equal("x → a ↵ b", r.ToString());
    }

    [Fact]
    public void ToString_ToNull_NeCrashePas()
    {
        var r = new ReplacementRule { From = "x", To = null! };
        Assert.Equal("x → ", r.ToString());
    }
}
