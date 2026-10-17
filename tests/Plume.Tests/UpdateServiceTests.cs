using System.IO;
using System.IO.Compression;
using System.Net;
using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class UpdateServiceTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "plume-update-tests-" + Guid.NewGuid().ToString("N"));

    public UpdateServiceTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [Theory]
    [InlineData("v5.4.8", "5.4.8")]
    [InlineData("5.4.8", "5.4.8")]
    [InlineData("V10.0.1", "10.0.1")]
    public void ParseTag_LitLaVersion(string tag, string attendu)
        => Assert.Equal(Version.Parse(attendu), UpdateService.ParseTag(tag));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("derniere")]
    [InlineData("v5.4")] // pas de numéro de correctif : pas une version Plume
    public void ParseTag_EtiquetteInvalide_Null(string? tag) => Assert.Null(UpdateService.ParseTag(tag));

    [Theory]
    [InlineData("5.4.7", "5.4.8", true)]
    [InlineData("5.4.7", "5.5.0", true)]
    [InlineData("5.4.9", "5.4.10", true)] // comparaison numérique, pas alphabétique
    [InlineData("5.4.8", "5.4.8", false)]
    [InlineData("5.4.8", "5.4.7", false)]
    public void IsNewer_CompareLesVersions(string courante, string derniere, bool attendu)
        => Assert.Equal(attendu, UpdateService.IsNewer(Version.Parse(courante), Version.Parse(derniere)));

    [Fact]
    public void IsNewer_IgnoreLeQuatriemeNumero()
        => Assert.False(UpdateService.IsNewer(new Version(5, 4, 8, 0), new Version(5, 4, 8)));

    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void ExtractSha256_TrouveLEmpreinte()
    {
        Assert.Equal(Hash, UpdateService.ExtractSha256("Nouveautés…\n\nsha256: " + Hash.ToUpperInvariant() + "\n"));
        Assert.Null(UpdateService.ExtractSha256("sha256: trop-court"));
        Assert.Null(UpdateService.ExtractSha256(null));
    }

    [Fact]
    public void ParseLatestRelease_LitVersionArchiveEtEmpreinte()
    {
        var json = "{\"tag_name\":\"v5.4.8\",\"body\":\"sha256: " + Hash + "\",\"assets\":["
                 + "{\"name\":\"notes.txt\",\"url\":\"https://api.github.com/a/1\"},"
                 + "{\"name\":\"Plume-5.4.8.zip\",\"url\":\"https://api.github.com/a/2\"}]}";
        var info = UpdateService.ParseLatestRelease(json);
        Assert.NotNull(info);
        Assert.Equal(new Version(5, 4, 8), info!.Version);
        Assert.Equal("v5.4.8", info.Tag);
        Assert.Equal("https://api.github.com/a/2", info.AssetApiUrl);
        Assert.Equal(Hash, info.Sha256);
    }

    [Theory]
    [InlineData("{\"tag_name\":\"v5.4.8\",\"assets\":[]}")]                      // aucune archive
    [InlineData("{\"tag_name\":\"brouillon\",\"assets\":[{\"name\":\"Plume-1.zip\",\"url\":\"u\"}]}")] // étiquette illisible
    [InlineData("pas du json")]
    public void ParseLatestRelease_ReleaseInexploitable_Null(string json)
        => Assert.Null(UpdateService.ParseLatestRelease(json));

    private string MakeZip(params (string Path, string Content)[] files)
    {
        var zip = Path.Combine(_dir, Guid.NewGuid().ToString("N") + ".zip");
        using var archive = ZipFile.Open(zip, ZipArchiveMode.Create);
        foreach (var (path, content) in files)
        {
            using var w = new StreamWriter(archive.CreateEntry(path).Open());
            w.Write(content);
        }
        return zip;
    }

    [Fact]
    public void ApplyArchive_RemplaceLExeEtLesRessources_GardeLAncienSousUnNomDeSecours()
    {
        var install = Path.Combine(_dir, "install");
        Directory.CreateDirectory(Path.Combine(install, "Resources"));
        File.WriteAllText(Path.Combine(install, "Plume.exe"), "ancien");
        File.WriteAllText(Path.Combine(install, "Resources", "plume.ico"), "ancienne icône");
        var zip = MakeZip(("Plume.exe", "nouveau"), ("Resources/plume.ico", "nouvelle icône"));

        UpdateService.ApplyArchive(zip, install);

        Assert.Equal("nouveau", File.ReadAllText(Path.Combine(install, "Plume.exe")));
        Assert.Equal("nouvelle icône", File.ReadAllText(Path.Combine(install, "Resources", "plume.ico")));
        Assert.Equal("ancien", File.ReadAllText(Path.Combine(install, "Plume.old.exe")));
        Assert.False(Directory.Exists(Path.Combine(install, "update-staging")));

        UpdateService.CleanupOld(install);
        Assert.False(File.Exists(Path.Combine(install, "Plume.old.exe")));
    }

    [Fact]
    public void ApplyArchive_SansPlumeExe_RefuseEtNeToucheARien()
    {
        var install = Path.Combine(_dir, "install2");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "Plume.exe"), "ancien");
        var zip = MakeZip(("autre.txt", "x"));

        Assert.Throws<UpdateException>(() => UpdateService.ApplyArchive(zip, install));

        Assert.Equal("ancien", File.ReadAllText(Path.Combine(install, "Plume.exe")));
        Assert.False(File.Exists(Path.Combine(install, "Plume.old.exe")));
    }

    [Fact]
    public void ApplyArchive_ArchiveIllisible_MessageFrancais()
    {
        var install = Path.Combine(_dir, "install3");
        Directory.CreateDirectory(install);
        File.WriteAllText(Path.Combine(install, "Plume.exe"), "ancien");
        var faux = Path.Combine(_dir, "faux.zip");
        File.WriteAllText(faux, "ceci n'est pas une archive");

        Assert.Throws<UpdateException>(() => UpdateService.ApplyArchive(faux, install));
        Assert.Equal("ancien", File.ReadAllText(Path.Combine(install, "Plume.exe")));
    }

    [Fact]
    public void Sha256Of_EmpreinteConnue()
    {
        var f = Path.Combine(_dir, "vide.bin");
        File.WriteAllBytes(f, Array.Empty<byte>());
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", UpdateService.Sha256Of(f));
    }

    private static string Sha256Hex(string s)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(s))).ToLowerInvariant();

    private static UpdateInfo Info(string? sha)
        => new(new Version(5, 4, 99), "v5.4.99", "https://api.github.com/repos/o/r/releases/assets/1", sha);

    [Fact]
    public async Task DownloadAsync_SansEmpreinte_RefuseSansTelecharger()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.OK, "archive");
        var dest = Path.Combine(_dir, "dl");
        var ex = await Assert.ThrowsAsync<UpdateException>(() => UpdateService.DownloadAsync(Info(null), dest, fake));
        Assert.Contains("empreinte", ex.Message);
        Assert.Null(fake.LastRequest);
        Assert.False(Directory.Exists(dest) && Directory.EnumerateFiles(dest).Any());
    }

    [Fact]
    public async Task DownloadAsync_EmpreinteFausse_RefuseEtSupprimeLArchive()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.OK, "archive");
        var dest = Path.Combine(_dir, "dl");
        var ex = await Assert.ThrowsAsync<UpdateException>(() => UpdateService.DownloadAsync(Info(Hash), dest, fake));
        Assert.Contains("ne correspond pas", ex.Message);
        Assert.Empty(Directory.EnumerateFiles(dest));
    }

    [Fact]
    public async Task DownloadAsync_EmpreinteJuste_RenvoieLeChemin()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.OK, "archive");
        var path = await UpdateService.DownloadAsync(Info(Sha256Hex("archive")), Path.Combine(_dir, "dl"), fake);
        Assert.Equal("archive", File.ReadAllText(path));
        Assert.Null(fake.LastRequest!.Headers.Authorization); // dépôt public : aucun jeton envoyé
    }

    [Fact]
    public async Task DownloadAsync_Refus_MessageFrancais()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.NotFound, "");
        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => UpdateService.DownloadAsync(Info(Hash), Path.Combine(_dir, "dl"), fake));
        Assert.Equal("Téléchargement refusé par GitHub (404).", ex.Message);
    }

    [Fact]
    public void ParseLatestRelease_ArchiveHorsApiGitHub_Ignoree()
    {
        var json = "{\"tag_name\":\"v5.4.99\",\"body\":\"sha256: " + Hash + "\",\"assets\":["
                 + "{\"name\":\"Plume-5.4.99.zip\",\"url\":\"https://exemple.invalid/Plume-5.4.99.zip\"}]}";
        Assert.Null(UpdateService.ParseLatestRelease(json));
    }

    [Fact]
    public async Task GetLatestAsync_DepotPublic_NEnvoieAucunJeton()
    {
        var json = "{\"tag_name\":\"v5.4.99\",\"body\":\"sha256: " + Hash + "\",\"assets\":["
                 + "{\"name\":\"Plume-5.4.99.zip\",\"url\":\"https://api.github.com/a/2\"}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var info = await UpdateService.GetLatestAsync(fake);
        Assert.Equal(new Version(5, 4, 99), info!.Version);
        Assert.Null(fake.LastRequest!.Headers.Authorization);
    }

    [Fact]
    public async Task GetLatestAsync_404_MessageSansMentionDeJeton()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.NotFound, "");
        var ex = await Assert.ThrowsAsync<UpdateException>(() => UpdateService.GetLatestAsync(fake));
        Assert.Equal("Aucune version publiée.", ex.Message);
    }
}
