using Plume.Models;
using Xunit;

namespace Plume.Tests;

public class SelectionStyleTests
{
    [Fact]
    public void SelectionStyle_Defauts_Sont_Sains()
    {
        var s = new SelectionStyle();
        Assert.Equal("", s.Id);
        Assert.Equal("", s.Name);
        Assert.Equal("", s.Prompt);
        Assert.False(s.WithVoice);
        Assert.False(s.IsFavorite);
        Assert.True(s.ShowInMenu);      // visible par défaut
        Assert.False(s.IsBuiltIn);
        Assert.Equal(0.2, s.Temperature);
    }

    [Fact]
    public void AppConfig_Defauts_V6()
    {
        var c = AppConfig.CreateDefault();
        Assert.NotNull(c.DictationPrompt);      // sera renseigné par StyleService.Normalize
        Assert.Equal(0.2, c.DictationTemperature);
        Assert.NotNull(c.SelectionStyles);
        Assert.Empty(c.SelectionStyles);        // vide avant normalisation
        Assert.True(c.ShowBadge);               // pastille active par défaut
        Assert.Null(c.BadgePosition);
    }
}
