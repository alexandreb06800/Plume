using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class OpenRouterExceptionTests
{
    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(410)]
    public void StatusHint_ModeleRetire_GuideVersLesReglages(int status)
        => Assert.Contains("Réglages → Général", OpenRouterException.StatusHint(status));

    [Fact]
    public void StatusHint_429_ParleDeLaLimite()
        => Assert.Contains("réessaie dans une minute", OpenRouterException.StatusHint(429));

    [Theory]
    [InlineData(401)]
    [InlineData(500)]
    public void StatusHint_AutreStatut_Vide(int status)
        => Assert.Equal("", OpenRouterException.StatusHint(status));

    [Fact]
    public void RawTextFallbackMessage_DitQueLeTexteEstBrutEtPourquoi()
    {
        var raison = "Échec du nettoyage (404)." + OpenRouterException.StatusHint(404);
        var msg = OpenRouterException.RawTextFallbackMessage(new OpenRouterException(raison));
        Assert.StartsWith("Nettoyage indisponible, texte brut inséré. ", msg);
        Assert.EndsWith(raison, msg);
        Assert.True(msg.Length <= 255); // limite des bulles (TrayIcon.ShowBalloon tronque au-delà)
    }
}
