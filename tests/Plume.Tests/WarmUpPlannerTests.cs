using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class WarmUpPlannerTests
{
    [Fact]
    public void Config100PourcentGroq_NePrechauffePasOpenRouter()
    {
        // Cas réel : STT Groq + nettoyage groq/… + assistant sur le modèle global, clé OpenRouter présente.
        var (groq, openRouter) = WarmUpPlanner.Targets(true, true, "groq/llama-3.3-70b-versatile", true, null);
        Assert.True(groq);
        Assert.False(openRouter);
    }

    [Fact]
    public void SttGroqEtNettoyageOpenRouter_PrechauffeLesDeux()
    {
        var (groq, openRouter) = WarmUpPlanner.Targets(true, true, "openai/gpt-4o-mini", true, null);
        Assert.True(groq);
        Assert.True(openRouter);
    }

    [Fact]
    public void SansCleGroq_SttPasseParOpenRouter()
    {
        var (groq, openRouter) = WarmUpPlanner.Targets(false, true, "openai/gpt-4o-mini", false, null);
        Assert.False(groq);
        Assert.True(openRouter);
    }

    [Fact]
    public void AssistantSurOpenRouter_PrechauffeOpenRouterMemeSiDicteeToutGroq()
    {
        var (_, openRouter) = WarmUpPlanner.Targets(true, true, "groq/llama-3.3-70b-versatile", true, "openai/gpt-4o-mini");
        Assert.True(openRouter);
    }

    [Fact]
    public void AssistantDesactive_SonModeleEstIgnore()
    {
        var (_, openRouter) = WarmUpPlanner.Targets(true, true, "groq/llama-3.3-70b-versatile", false, "openai/gpt-4o-mini");
        Assert.False(openRouter);
    }

    [Fact]
    public void CleAbsente_JamaisPrechauffe()
    {
        var (groq, openRouter) = WarmUpPlanner.Targets(false, false, "openai/gpt-4o-mini", true, null);
        Assert.False(groq);
        Assert.False(openRouter);
    }

    [Fact]
    public void OpenRouter_EndpointLeger_PasLaListeDesModeles()
    {
        Assert.DoesNotContain("/models", WarmUpPlanner.OpenRouterUrl);
    }
}
