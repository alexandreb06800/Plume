using Microsoft.Win32;
using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class StartupServiceTests
{
    [Fact]
    public void SetEnabled_ActivePuisDesactive()
    {
        // Ce test écrit dans le VRAI registre utilisateur : on sauvegarde puis on restaure l'entrée
        // réelle. Sans restauration, chaque `dotnet test` supprimait le « Démarrer avec Windows »
        // du poste de dev (bug constaté le 2026-07-06 : la case ne restait jamais cochée).
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        var original = key.GetValue("Plume");
        try
        {
            StartupService.SetEnabled(true, @"C:\fake\Plume.exe");
            Assert.True(StartupService.IsEnabled());

            StartupService.SetEnabled(false, @"C:\fake\Plume.exe");
            Assert.False(StartupService.IsEnabled());
        }
        finally
        {
            if (original is not null) key.SetValue("Plume", original);
            else key.DeleteValue("Plume", throwOnMissingValue: false);
        }
    }

    [Theory]
    [InlineData(@"C:\Users\x\OneDrive - Acme\Programmes\Plume\Plume.exe", true)]
    [InlineData(@"C:\Users\x\OneDrive\Plume\Plume.exe", true)]
    [InlineData(@"C:\Users\x\AppData\Local\Plume\Plume.exe", false)]
    [InlineData(@"C:\Program Files\Plume\Plume.exe", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsRunningFromOneDrive_DetecteLesCheminsSynchronises(string? exe, bool attendu)
        => Assert.Equal(attendu, StartupService.IsRunningFromOneDrive(exe));

    // --- ResolveRunPath : jamais un chemin OneDrive dans la clé Run ---

    private const string Installe = @"C:\Users\x\AppData\Local\Plume\Plume.exe";

    [Fact]
    public void ResolveRunPath_CheminLocal_ConserveLeProcessus()
        => Assert.Equal(@"C:\Users\x\AppData\Local\Plume\Plume.exe",
            StartupService.ResolveRunPath(@"C:\Users\x\AppData\Local\Plume\Plume.exe", Installe, _ => true));

    [Fact]
    public void ResolveRunPath_ProcessusSousOneDrive_PrefereLExeInstalle()
        => Assert.Equal(Installe,
            StartupService.ResolveRunPath(@"C:\Users\x\OneDrive - Acme\Programmes\Plume\bin\Plume.exe", Installe, _ => true));

    [Fact]
    public void ResolveRunPath_OneDriveEtPasDExeInstalle_GardeLeProcessus()
        => Assert.Equal(@"C:\Users\x\OneDrive\Plume\Plume.exe",
            StartupService.ResolveRunPath(@"C:\Users\x\OneDrive\Plume\Plume.exe", Installe, _ => false));

    [Fact]
    public void ResolveRunPath_ProcessusInconnu_RabatSurLExeInstalle()
        => Assert.Equal(Installe, StartupService.ResolveRunPath(null, Installe, _ => true));

    [Theory]
    [InlineData(@"C:\Projets\Plume\src\Plume\bin\Debug\net10.0-windows\Plume.exe", true)]
    [InlineData(@"C:\Projets\Plume\src\Plume\bin\Release\net10.0-windows\win-x64\publish\Plume.exe", true)]
    [InlineData(@"C:\Users\x\AppData\Local\Plume\Plume.exe", false)]
    [InlineData(null, false)]
    public void IsDevBuildPath_DetecteLesDossiersDeCompilation(string? exe, bool attendu)
        => Assert.Equal(attendu, StartupService.IsDevBuildPath(exe));

    [Fact]
    public void ResolveRunPath_ExeDeDeveloppement_PrefereLExeInstalle()
        => Assert.Equal(Installe, StartupService.ResolveRunPath(
            @"C:\Projets\Plume\src\Plume\bin\Debug\net10.0-windows\Plume.exe", Installe, _ => true));

    [Fact]
    public void ResolveRunPath_ExeDeDeveloppementSansExeInstalle_GardeLeProcessus()
        => Assert.Equal(@"C:\Projets\Plume\src\Plume\bin\Debug\net10.0-windows\Plume.exe", StartupService.ResolveRunPath(
            @"C:\Projets\Plume\src\Plume\bin\Debug\net10.0-windows\Plume.exe", Installe, _ => false));
}
