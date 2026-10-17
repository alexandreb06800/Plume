using Plume.Services;
using Xunit;

namespace Plume.Tests;

/// <summary>
/// Détection « un modèle plus récent existe » par heuristique famille/version
/// (ids bruts renvoyés par l'API Groq). Logique pure, sans réseau.
/// </summary>
public class ModelUpdateServiceTests
{
    [Fact]
    public void FindNewer_VersionSuperieure_MemeFamille_EstSignalee()
    {
        var newer = ModelUpdateService.FindNewer(
            "whisper-large-v3-turbo",
            new[] { "whisper-large-v3-turbo", "whisper-large-v4-turbo" });

        Assert.Equal(new[] { "whisper-large-v4-turbo" }, newer);
    }

    [Fact]
    public void FindNewer_AucuneVersionSuperieure_RenvoieVide()
    {
        var newer = ModelUpdateService.FindNewer(
            "whisper-large-v3-turbo",
            new[] { "whisper-large-v3-turbo", "whisper-large-v2" });

        Assert.Empty(newer);
    }

    [Fact]
    public void FindNewer_MemeVersion_NonSignalee()
    {
        var newer = ModelUpdateService.FindNewer(
            "llama-3.3-70b-versatile",
            new[] { "llama-3.3-70b-versatile" });

        Assert.Empty(newer);
    }

    [Fact]
    public void FindNewer_VarianteDifferente_NonSignalee()
    {
        // « turbo » retiré = autre déclinaison, pas une montée de version.
        var newer = ModelUpdateService.FindNewer(
            "whisper-large-v3-turbo",
            new[] { "whisper-large-v4" });

        Assert.Empty(newer);
    }

    [Fact]
    public void FindNewer_TailleDifferente_NonSignalee()
    {
        // 8b ≠ 70b : ce n'est pas le même modèle, on ne propose pas.
        var newer = ModelUpdateService.FindNewer(
            "llama-3.3-70b-versatile",
            new[] { "llama-3.4-8b-versatile" });

        Assert.Empty(newer);
    }

    [Fact]
    public void FindNewer_FamilleDifferente_Ignoree()
    {
        var newer = ModelUpdateService.FindNewer(
            "whisper-large-v3-turbo",
            new[] { "llama-3.3-70b-versatile", "gpt-oss-120b" });

        Assert.Empty(newer);
    }

    [Fact]
    public void FindNewer_VersionDecimale_CompareCorrectement()
    {
        // 3.3 > 3.1 : montée de version mineure détectée.
        var newer = ModelUpdateService.FindNewer(
            "llama-3.1-8b-instant",
            new[] { "llama-3.3-8b-instant" });

        Assert.Equal(new[] { "llama-3.3-8b-instant" }, newer);
    }

    [Fact]
    public void FindNewer_VersionMajeure_BatVersionMineure()
    {
        // 4 > 3.3 (majeure l'emporte).
        var newer = ModelUpdateService.FindNewer(
            "llama-3.3-70b-versatile",
            new[] { "llama-4-70b-versatile" });

        Assert.Equal(new[] { "llama-4-70b-versatile" }, newer);
    }

    [Fact]
    public void FindNewer_ConservePrefixeGroq()
    {
        // Le modèle de nettoyage est stocké préfixé « groq/ » : la suggestion doit l'être aussi.
        var newer = ModelUpdateService.FindNewer(
            "groq/llama-3.3-70b-versatile",
            new[] { "llama-3.4-70b-versatile" });

        Assert.Equal(new[] { "groq/llama-3.4-70b-versatile" }, newer);
    }

    [Fact]
    public void FindNewer_ConservePrefixeOpenai()
    {
        var newer = ModelUpdateService.FindNewer(
            "openai/whisper-large-v3-turbo",
            new[] { "whisper-large-v4-turbo" });

        Assert.Equal(new[] { "openai/whisper-large-v4-turbo" }, newer);
    }

    [Fact]
    public void FindNewer_PlusieursVersions_TriDecroissant()
    {
        var newer = ModelUpdateService.FindNewer(
            "whisper-large-v3-turbo",
            new[] { "whisper-large-v4-turbo", "whisper-large-v5-turbo" });

        Assert.Equal(new[] { "whisper-large-v5-turbo", "whisper-large-v4-turbo" }, newer);
    }

    [Fact]
    public void FindNewer_ModeleCourantSansVersion_RenvoieVide()
    {
        var newer = ModelUpdateService.FindNewer(
            "gpt-oss-120b",
            new[] { "gpt-oss-20b", "gpt-oss-120b" });

        Assert.Empty(newer);
    }

    [Fact]
    public void FindNewer_Insensible_Casse_Espaces()
    {
        var newer = ModelUpdateService.FindNewer(
            "  Whisper-Large-V3-Turbo  ",
            new[] { "whisper-large-v4-turbo" });

        Assert.Equal(new[] { "whisper-large-v4-turbo" }, newer);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FindNewer_EntreeVide_RenvoieVide_SansCrash(string? current)
    {
        var newer = ModelUpdateService.FindNewer(current!, new[] { "whisper-large-v4-turbo" });
        Assert.Empty(newer);
    }

    [Fact]
    public void FindNewer_DisponiblesNull_RenvoieVide_SansCrash()
    {
        var newer = ModelUpdateService.FindNewer("whisper-large-v3-turbo", null!);
        Assert.Empty(newer);
    }

    [Fact]
    public void FindNewer_IgnoreLeModeleCourantLuiMeme()
    {
        // L'id courant présent dans la liste ne se signale pas lui-même.
        var newer = ModelUpdateService.FindNewer(
            "whisper-large-v3-turbo",
            new[] { "whisper-large-v3-turbo" });

        Assert.Empty(newer);
    }

    [Fact]
    public void FindNewer_QwenAvecOrganisation_VersionCollee_EstSignalee()
        => Assert.Equal(new[] { "groq/qwen/qwen3.9-27b" },
            ModelUpdateService.FindNewer("groq/qwen/qwen3.8-27b", new[] { "qwen/qwen3.8-27b", "qwen/qwen3.9-27b" }));

    [Fact]
    public void FindNewer_QwenTailleDifferente_NonSignalee()
        => Assert.Empty(ModelUpdateService.FindNewer("groq/qwen/qwen3.8-27b", new[] { "qwen/qwen3.9-32b" }));

    [Fact]
    public void FindNewer_QwenAutreOrganisation_NonSignalee()
        => Assert.Empty(ModelUpdateService.FindNewer("groq/qwen/qwen3.8-27b", new[] { "autre/qwen3.9-27b" }));

    [Theory]
    [InlineData("groq/openai/gpt-oss-120b", false, "openai/gpt-oss-120b")]
    [InlineData("openai/gpt-4o-mini", false, null)]
    [InlineData("openai/whisper-large-v3", true, "whisper-large-v3")]
    [InlineData("whisper-large-v3", true, "whisper-large-v3")]
    public void GroqBareId_DonneLIdListeParGroq(string configure, bool isStt, string? attendu)
        => Assert.Equal(attendu, ModelUpdateService.GroqBareId(configure, isStt));

    [Fact]
    public void IsAvailable_CompareSansCasse()
    {
        var ids = new[] { "openai/gpt-oss-120b", "whisper-large-v3" };
        Assert.True(ModelUpdateService.IsAvailable("OpenAI/GPT-OSS-120b", ids));
        Assert.False(ModelUpdateService.IsAvailable("qwen/qwen3.6-27b", ids));
    }
}
