using Plume.Services;
using Xunit;

namespace Plume.Tests;

/// <summary>Parsing de la réponse /models de Groq (logique pure, sans réseau).</summary>
public class GroqModelsClientTests
{
    [Fact]
    public void ParseModelIds_ReponseStandard_RenvoieLesIds()
    {
        const string json = """
        {
          "object": "list",
          "data": [
            { "id": "whisper-large-v3-turbo", "object": "model" },
            { "id": "llama-3.3-70b-versatile", "object": "model" }
          ]
        }
        """;

        var ids = GroqModelsClient.ParseModelIds(json);

        Assert.Equal(new[] { "whisper-large-v3-turbo", "llama-3.3-70b-versatile" }, ids);
    }

    [Fact]
    public void ParseModelIds_DataVide_RenvoieVide()
        => Assert.Empty(GroqModelsClient.ParseModelIds("""{ "object": "list", "data": [] }"""));

    [Fact]
    public void ParseModelIds_SansChampData_RenvoieVide()
        => Assert.Empty(GroqModelsClient.ParseModelIds("""{ "object": "list" }"""));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("pas du json")]
    [InlineData("{")]
    public void ParseModelIds_JsonInvalide_RenvoieVide_SansCrash(string json)
        => Assert.Empty(GroqModelsClient.ParseModelIds(json));

    [Fact]
    public void ParseModelIds_IgnoreLesEntreesSansId()
    {
        const string json = """
        { "data": [ { "object": "model" }, { "id": "llama-3.1-8b-instant" } ] }
        """;

        Assert.Equal(new[] { "llama-3.1-8b-instant" }, GroqModelsClient.ParseModelIds(json));
    }
}
