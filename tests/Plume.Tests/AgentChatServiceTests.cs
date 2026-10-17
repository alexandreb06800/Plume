using System.Net;
using System.Net.Http;
using System.Text.Json;
using Plume.Models;
using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class AgentChatServiceTests
{
    private static readonly DateTime SampleNow = new(2026, 6, 29, 14, 32, 0); // lundi 29 juin 2026

    [Fact]
    public void BuildMessages_InjecteSystemQuestionEtHistorique()
    {
        var s = new AgentSession();
        s.AddTurn("Quel jour est-il ?", "Lundi.");
        var msgs = AgentChatService.BuildMessages(s, "Et demain ?", SampleNow, null);

        // system + (user/assistant du tour passé) + nouvelle question = 4 messages.
        Assert.Equal(4, msgs.Length);
        var json = JsonSerializer.Serialize(msgs);
        Assert.Contains("\"role\":\"system\"", json);
        Assert.Contains("assistant vocal", json);          // rôle décrit en FR
        Assert.Contains("2026", json);                       // date injectée
        Assert.Contains("Quel jour est-il ?", json);         // question du tour passé
        Assert.Contains("Lundi.", json);                     // réponse du tour passé
        Assert.Contains("Et demain ?", json);                // nouvelle question
    }

    [Fact]
    public void BuildMessages_DerniereEntree_EstLaNouvelleQuestion()
    {
        var msgs = AgentChatService.BuildMessages(new AgentSession(), "Ma question.", SampleNow, null);
        var last = JsonSerializer.Serialize(msgs[^1]);
        Assert.Contains("\"role\":\"user\"", last);
        Assert.Contains("Ma question.", last);
    }

    [Fact]
    public void BuildMessages_AvecVocabulaire_InjecteLesTermes()
    {
        var msgs = AgentChatService.BuildMessages(new AgentSession(), "q", SampleNow, new[] { "MotMetier" });
        Assert.Contains("MotMetier", JsonSerializer.Serialize(msgs));
    }

    [Fact]
    public async Task AskAsync_ReponseOK_RetourneContenu()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"Lundi.\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new AgentChatService(new HttpClient(fake));

        var result = await svc.AskAsync(new AgentSession(), "Quel jour est-il ?", SampleNow,
            "openai/gpt-4o-mini", "sk-x", null, null, CancellationToken.None);

        Assert.Equal("Lundi.", result);
        Assert.Contains("openrouter.ai", fake.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task AskAsync_ModeleGroq_RouteVersGroqSansPrefixe()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new AgentChatService(new HttpClient(fake));

        await svc.AskAsync(new AgentSession(), "q", SampleNow,
            "groq/llama-3.3-70b-versatile", "sk-openrouter", "gsk-groq", null, CancellationToken.None);

        Assert.Contains("api.groq.com", fake.LastRequest!.RequestUri!.ToString());
        Assert.Equal("gsk-groq", fake.LastRequest.Headers.Authorization!.Parameter);
        Assert.Contains("\"llama-3.3-70b-versatile\"", fake.LastRequestBody);
        Assert.DoesNotContain("groq/llama", fake.LastRequestBody);
    }

    [Fact]
    public async Task AskAsync_401_LeveExceptionUnauthorized()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.Unauthorized, "no");
        var svc = new AgentChatService(new HttpClient(fake));
        var ex = await Assert.ThrowsAsync<OpenRouterException>(() =>
            svc.AskAsync(new AgentSession(), "q", SampleNow, "m", "k", null, null, CancellationToken.None));
        Assert.True(ex.IsUnauthorized);
    }

    [Fact]
    public async Task AskAsync_Erreur500_LeveException()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.InternalServerError, "boom");
        var svc = new AgentChatService(new HttpClient(fake));
        await Assert.ThrowsAsync<OpenRouterException>(() =>
            svc.AskAsync(new AgentSession(), "q", SampleNow, "m", "k", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task AskAsync_ContenuVide_LeveExceptionReponseInattendue()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new AgentChatService(new HttpClient(fake));

        var ex = await Assert.ThrowsAsync<OpenRouterException>(() =>
            svc.AskAsync(new AgentSession(), "q", SampleNow, "m", "k", null, null, CancellationToken.None));
        Assert.Contains("inattendue", ex.Message);
    }

    [Fact]
    public async Task AskAsync_ModeleDisparu404_GuideVersLesReglages()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.NotFound, "{\"error\":{\"message\":\"model_decommissioned\"}}");
        var svc = new AgentChatService(new HttpClient(fake));

        var ex = await Assert.ThrowsAsync<OpenRouterException>(() =>
            svc.AskAsync(new AgentSession(), "question", SampleNow, "m", "k", null, null, CancellationToken.None));
        Assert.Contains("404", ex.Message);
        Assert.Contains("Réglages", ex.Message);
    }

    [Fact]
    public async Task AskAsync_GptOssSurGroq_ReglageLowEtReflexionRetiree()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.OK, "{\"choices\":[{\"message\":{\"content\":\"<think>x</think>Lundi.\"}}]}");
        var result = await new AgentChatService(new HttpClient(fake)).AskAsync(new AgentSession(), "Quel jour ?", SampleNow,
            "groq/openai/gpt-oss-120b", "", "gsk-x", null, CancellationToken.None);
        Assert.Equal("Lundi.", result);
        Assert.Contains("\"reasoning_effort\":\"low\"", fake.LastRequestBody);
    }
}
