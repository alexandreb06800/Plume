using System.IO;
using System.Threading.Tasks;
using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class DiagLogServiceTests
{
    private static DiagLogService NewServiceInTemp(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "PlumeTest_diag_" + Guid.NewGuid().ToString("N"));
        return new DiagLogService(dir);
    }

    [Fact]
    public async Task Log_Desactive_NecritAucunFichier()
    {
        var svc = NewServiceInTemp(out var dir);
        await svc.Log(false, "Transcription", "1200 ms");
        Assert.False(File.Exists(Path.Combine(dir, "diag.log")));
    }

    [Fact]
    public async Task Log_Active_EcritUneLigneAvecEtapeEtDetail()
    {
        var svc = NewServiceInTemp(out var dir);
        await svc.Log(true, "Transcription", "1200 ms (Groq direct)");

        var content = File.ReadAllText(Path.Combine(dir, "diag.log"));
        Assert.Contains("Transcription", content);
        Assert.Contains("1200 ms (Groq direct)", content);
    }

    [Fact]
    public void WriteNow_JournalTropGros_RotationVersOldLog()
    {
        var svc = NewServiceInTemp(out var dir);
        Directory.CreateDirectory(dir);
        var logPath = Path.Combine(dir, "diag.log");
        File.WriteAllText(logPath, new string('x', 600_000)); // dépasse le plafond ~500 Ko

        svc.WriteNow("Nettoyage", "300 ms");

        var oldPath = Path.Combine(dir, "diag.old.log");
        Assert.True(File.Exists(oldPath));
        Assert.True(new FileInfo(oldPath).Length >= 600_000);
        var newContent = File.ReadAllText(logPath);
        Assert.Contains("Nettoyage", newContent);
        Assert.True(newContent.Length < 600_000); // le nouveau fichier repart de zéro
    }

    [Fact]
    public void WriteNow_ErreurEcriture_NeCrashePas()
    {
        // Dossier impossible à créer (chemin sous un fichier existant) : ne doit jamais lever.
        var fakeParent = Path.Combine(Path.GetTempPath(), "PlumeTest_diag_" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(fakeParent, "je suis un fichier, pas un dossier");
        var svc = new DiagLogService(Path.Combine(fakeParent, "sous-dossier"));

        svc.WriteNow("Nettoyage", "300 ms"); // ne doit pas lever

        File.Delete(fakeParent);
    }

    [Fact]
    public void LogNow_Active_EcritAvantDeRendreLaMain()
    {
        var svc = NewServiceInTemp(out var dir);
        svc.LogNow(true, "Exception fatale (AppDomain)", "System.Exception: boum");
        Assert.Contains("Exception fatale (AppDomain)", File.ReadAllText(Path.Combine(dir, "diag.log")));
    }

    [Fact]
    public void LogNow_Desactive_NEcritRien()
    {
        var svc = NewServiceInTemp(out var dir);
        svc.LogNow(false, "Exception fatale (AppDomain)");
        Assert.False(File.Exists(Path.Combine(dir, "diag.log")));
    }
}
