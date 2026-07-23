using System.Linq;
using Plume.Models;
using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class StyleMenuTests
{
    private static SelectionStyle S(string id, bool fav = false, bool show = true)
        => new() { Id = id, Name = id, Prompt = "p", IsFavorite = fav, ShowInMenu = show };

    [Fact]
    public void MenuOrder_Favoris_En_Tete_Autres_Ensuite()
    {
        var styles = new[] { S("a"), S("b", fav: true), S("c"), S("d", fav: true) };
        var (favs, others) = StyleService.MenuOrder(styles);
        Assert.Equal(new[] { "b", "d" }, favs.Select(s => s.Id));
        Assert.Equal(new[] { "a", "c" }, others.Select(s => s.Id));
    }

    [Fact]
    public void MenuOrder_Ignore_Les_Masques()
    {
        var styles = new[] { S("a", show: false), S("b", fav: true), S("c", fav: true, show: false) };
        var (favs, others) = StyleService.MenuOrder(styles);
        Assert.Equal(new[] { "b" }, favs.Select(s => s.Id));
        Assert.Empty(others);
    }

    [Fact]
    public void MenuOrder_Conserve_L_Ordre_D_Origine()
    {
        var styles = new[] { S("z"), S("y"), S("x") };
        var (_, others) = StyleService.MenuOrder(styles);
        Assert.Equal(new[] { "z", "y", "x" }, others.Select(s => s.Id));
    }
}
