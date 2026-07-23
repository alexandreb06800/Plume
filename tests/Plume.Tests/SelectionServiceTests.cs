using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class SelectionServiceTests
{
    private const string Sentinel = "PLUME_SENTINEL";

    [Fact]
    public void InterpretCapture_PressePapiersInchange_RetourneNull()
    {
        // Ctrl+C n'a rien copié (aucune sélection) : le sentinel est toujours là.
        Assert.Null(SelectionService.InterpretCapture(Sentinel, Sentinel));
    }

    [Fact]
    public void InterpretCapture_PressePapiersVide_RetourneNull()
    {
        Assert.Null(SelectionService.InterpretCapture(Sentinel, null));
        Assert.Null(SelectionService.InterpretCapture(Sentinel, ""));
    }

    [Fact]
    public void InterpretCapture_TexteCopie_RetourneLeTexte()
    {
        Assert.Equal("Bonjour Marie", SelectionService.InterpretCapture(Sentinel, "Bonjour Marie"));
    }
}
