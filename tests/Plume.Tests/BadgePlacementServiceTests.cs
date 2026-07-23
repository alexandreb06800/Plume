using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class BadgePlacementServiceTests
{
    [Fact]
    public void DefaultPosition_CoinBasDroit_AvecMarges()
    {
        var (x, y) = BadgePlacementService.DefaultPosition(
            workAreaLeft: 0, workAreaTop: 0, workAreaWidth: 1920, workAreaHeight: 1040,
            badgeWidth: 72, badgeHeight: 72);
        Assert.Equal(1920 - 72 - 24, x);
        Assert.Equal(1040 - 72 - 96, y);
    }

    [Fact]
    public void DefaultPosition_EcranSecondaireDecale_RelatifAWorkArea()
    {
        // Écran secondaire à droite du principal : la zone de travail est décalée en X.
        var (x, y) = BadgePlacementService.DefaultPosition(
            workAreaLeft: 1920, workAreaTop: 0, workAreaWidth: 1920, workAreaHeight: 1040,
            badgeWidth: 72, badgeHeight: 72);
        Assert.Equal(1920 + 1920 - 72 - 24, x);
        Assert.Equal(1040 - 72 - 96, y);
    }

    [Fact]
    public void Clamp_PositionDejaDansLaZone_Inchangee()
    {
        var (x, y) = BadgePlacementService.Clamp(100, 100, 72, 72,
            workAreaLeft: 0, workAreaTop: 0, workAreaWidth: 1920, workAreaHeight: 1040);
        Assert.Equal(100, x);
        Assert.Equal(100, y);
    }

    [Fact]
    public void Clamp_PositionOrphelineApresDebranchementEcran_RamenerDansLaZone()
    {
        // Position mémorisée sur un ancien écran plus large : hors de la zone visible actuelle.
        var (x, y) = BadgePlacementService.Clamp(2400, 1200, 72, 72,
            workAreaLeft: 0, workAreaTop: 0, workAreaWidth: 1920, workAreaHeight: 1040);
        Assert.Equal(1920 - 72, x);
        Assert.Equal(1040 - 72, y);
    }

    [Fact]
    public void Clamp_PositionNegative_RamenerAuBordGaucheHaut()
    {
        var (x, y) = BadgePlacementService.Clamp(-500, -300, 72, 72,
            workAreaLeft: 0, workAreaTop: 0, workAreaWidth: 1920, workAreaHeight: 1040);
        Assert.Equal(0, x);
        Assert.Equal(0, y);
    }

    [Fact]
    public void Clamp_ZoneDeTravailDecalee_RespecteLOrigine()
    {
        // Écran secondaire à gauche du principal (zone de travail en X négatif).
        var (x, y) = BadgePlacementService.Clamp(-100, 100, 72, 72,
            workAreaLeft: -1920, workAreaTop: 0, workAreaWidth: 1920, workAreaHeight: 1040);
        Assert.Equal(-100, x); // dans [-1920, 1920-72] → inchangé
        Assert.Equal(100, y);
    }

    [Fact]
    public void Clamp_ZoneMinusculeQueLaPastille_NePasSortirDuCoteMoins()
    {
        // Cas limite : zone de travail plus petite que la pastille elle-même.
        var (x, y) = BadgePlacementService.Clamp(500, 500, 72, 72,
            workAreaLeft: 0, workAreaTop: 0, workAreaWidth: 40, workAreaHeight: 40);
        Assert.Equal(0, x);
        Assert.Equal(0, y);
    }
}
