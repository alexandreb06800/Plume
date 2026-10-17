using Plume.Models;
using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class ModelCatalogTests
{
    [Fact]
    public void Listes_NonVides()
    {
        Assert.NotEmpty(ModelCatalog.Stt);
        Assert.NotEmpty(ModelCatalog.Cleanup);
    }

    [Fact]
    public void ChaqueEntree_ABienSesChamps()
    {
        foreach (var m in ModelCatalog.Stt.Concat(ModelCatalog.Cleanup))
        {
            Assert.False(string.IsNullOrWhiteSpace(m.Id));
            Assert.False(string.IsNullOrWhiteSpace(m.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(m.PriceLabel));
            Assert.False(string.IsNullOrWhiteSpace(m.CostPerDictationLabel));
            Assert.InRange(m.Reliability, 1, 5);
        }
    }

    [Fact]
    public void Stt_SontDeTypeStt_EtCleanupDeTypeCleanup()
    {
        Assert.All(ModelCatalog.Stt, m => Assert.Equal(ModelKind.Stt, m.Kind));
        Assert.All(ModelCatalog.Cleanup, m => Assert.Equal(ModelKind.Cleanup, m.Kind));
    }

    [Fact]
    public void Find_IdConnu_RenvoieEntree()
    {
        var first = ModelCatalog.Stt[0];
        Assert.Equal(first, ModelCatalog.Find(first.Id));
    }

    [Fact]
    public void Find_IdInconnu_RenvoieNull()
        => Assert.Null(ModelCatalog.Find("modele/inexistant-xyz"));

    [Fact]
    public void Contient_LesDefautsHistoriques()
    {
        Assert.NotNull(ModelCatalog.Find("openai/whisper-large-v3-turbo"));
        Assert.NotNull(ModelCatalog.Find("openai/gpt-4o-mini"));
    }

    [Fact]
    public void Cleanup_ContientLesModelesRetenus()
    {
        // Catalogue volontairement restreint : Qwen 3.8 et GPT-OSS 120B (Groq), GPT-4o mini (OpenRouter).
        Assert.NotNull(ModelCatalog.Find("openai/gpt-4o-mini"));
        Assert.NotNull(ModelCatalog.Find("groq/qwen/qwen3.8-27b"));
        Assert.Equal(3, ModelCatalog.Cleanup.Count);
    }

    [Fact]
    public void Cleanup_NeContientPlusLesModelesRetires()
    {
        Assert.Null(ModelCatalog.Find("anthropic/claude-haiku-4-5"));
        Assert.Null(ModelCatalog.Find("deepseek/deepseek-chat"));
        Assert.Null(ModelCatalog.Find("moonshotai/kimi-k2"));
        Assert.Null(ModelCatalog.Find("qwen/qwen-2.5-72b-instruct"));
        // Arrêtés par leurs fournisseurs (vérifié le 2026-10-09 : 404) : ne plus les proposer.
        Assert.Null(ModelCatalog.Find("groq/llama-3.3-70b-versatile"));
        Assert.Null(ModelCatalog.Find("groq/llama-3.1-8b-instant"));
        Assert.Null(ModelCatalog.Find("google/gemini-2.0-flash-001"));
    }

    [Fact]
    public void Cleanup_ContientLesModelesGroqGratuits()
    {
        // Modèles de nettoyage exécutés sur Groq (préfixe « groq/ »).
        Assert.NotNull(ModelCatalog.Find("groq/qwen/qwen3.8-27b"));
        Assert.NotNull(ModelCatalog.Find("groq/openai/gpt-oss-120b"));
        Assert.All(ModelCatalog.Cleanup.Where(m => m.Id.StartsWith("groq/", StringComparison.Ordinal)),
            m => Assert.Contains("Gratuit", m.PriceLabel));
    }

    [Fact]
    public void DefautGroq_EstUnModeleProduction_GptOss120b()
        => Assert.Equal("groq/openai/gpt-oss-120b", ModelCatalog.DefaultGroqCleanupId);

    [Theory]
    [InlineData("groq/llama-3.3-70b-versatile", "groq/openai/gpt-oss-120b")]
    [InlineData("groq/llama-3.1-8b-instant", "groq/openai/gpt-oss-120b")]
    [InlineData("groq/qwen/qwen3-32b", "groq/openai/gpt-oss-120b")]
    [InlineData("groq/qwen/qwen3.6-27b", "groq/qwen/qwen3.8-27b")]
    [InlineData("google/gemini-2.0-flash-001", "openai/gpt-4o-mini")]
    public void ReplacementFor_ModeleRetire_RenvoieSonRemplacant(string retire, string attendu)
        => Assert.Equal(attendu, ModelCatalog.ReplacementFor(retire));

    [Theory]
    [InlineData("groq/openai/gpt-oss-120b")]
    [InlineData("mon/modele-perso")]
    [InlineData("")]
    [InlineData(null)]
    public void ReplacementFor_ModeleActifOuInconnu_Null(string? id)
        => Assert.Null(ModelCatalog.ReplacementFor(id));
}
