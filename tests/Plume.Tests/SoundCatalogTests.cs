using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class SoundCatalogTests
{
    [Fact]
    public void All_ContientAucunEtDeuxSons()
    {
        Assert.Equal(3, SoundCatalog.All.Count);
        Assert.Equal(SoundCatalog.None, SoundCatalog.All[0].Id);
        Assert.Equal("Aucun", SoundCatalog.All[0].DisplayName);
    }

    [Fact]
    public void All_IdsUniques()
    {
        var ids = SoundCatalog.All.Select(s => s.Id).ToList();
        Assert.Equal(ids.Distinct().Count(), ids.Count);
    }

    [Fact]
    public void All_ContientDiscretEtClair()
    {
        Assert.Contains(SoundCatalog.All, s => s.Id == "discret" && s.DisplayName == "Discret");
        Assert.Contains(SoundCatalog.All, s => s.Id == "clair" && s.DisplayName == "Clair");
    }
}
