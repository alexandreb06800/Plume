using System.Net;
using System.Net.Http;
using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class TranscriptionServiceTests
{
    [Fact]
    public async Task TranscribeAsync_ReponseOK_RetourneTexte()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.OK, "{\"text\":\"bonjour le monde\"}");
        var svc = new TranscriptionService(new HttpClient(fake));

        var text = await svc.TranscribeAsync(new byte[] { 1, 2, 3 }, "openai/whisper-1", "sk-x", null, null, CancellationToken.None);

        Assert.Equal("bonjour le monde", text);
        Assert.Contains("/audio/transcriptions", fake.LastRequest!.RequestUri!.ToString());
        Assert.Equal("Bearer", fake.LastRequest.Headers.Authorization!.Scheme);
        Assert.Contains("\"language\"", fake.LastRequestBody);
        // OpenRouter exige le champ "input_audio" (objet data+format), pas "audio".
        Assert.Contains("\"input_audio\"", fake.LastRequestBody);
        Assert.Contains("\"format\"", fake.LastRequestBody);
    }

    [Fact]
    public async Task TranscribeAsync_401_LeveExceptionUnauthorized()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.Unauthorized, "{\"error\":\"no\"}");
        var svc = new TranscriptionService(new HttpClient(fake));

        var ex = await Assert.ThrowsAsync<OpenRouterException>(() =>
            svc.TranscribeAsync(new byte[] { 1 }, "m", "bad", null, null, CancellationToken.None));
        Assert.True(ex.IsUnauthorized);
    }

    [Fact]
    public async Task TranscribeAsync_AvecVocabulaire_AjouteProviderPrompt()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.OK, "{\"text\":\"ok\"}");
        var svc = new TranscriptionService(new HttpClient(fake));

        await svc.TranscribeAsync(new byte[] { 1 }, "m", "k", null, new[] { "MotMetier", "SFTP" }, CancellationToken.None);

        Assert.Contains("\"provider\"", fake.LastRequestBody);
        Assert.Contains("MotMetier", fake.LastRequestBody);
    }

    [Fact]
    public async Task TranscribeAsync_SansVocabulaire_EpingleProviderRapide()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.OK, "{\"text\":\"ok\"}");
        var svc = new TranscriptionService(new HttpClient(fake));

        await svc.TranscribeAsync(new byte[] { 1 }, "m", "k", null, null, CancellationToken.None);

        // On épingle Groq (rapide) avec repli autorisé, même sans vocabulaire.
        Assert.Contains("\"provider\"", fake.LastRequestBody);
        Assert.Contains("Groq", fake.LastRequestBody);
        Assert.Contains("allow_fallbacks", fake.LastRequestBody);
    }

    [Fact]
    public async Task TranscribeAsync_AvecCleGroq_AppelleGroqEnMultipart()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.OK, "{\"text\":\"salut\"}");
        var svc = new TranscriptionService(new HttpClient(fake));

        var text = await svc.TranscribeAsync(new byte[] { 1, 2 }, "openai/whisper-large-v3-turbo",
            "sk-openrouter", "gsk-groq", null, CancellationToken.None);

        Assert.Equal("salut", text);
        // Cible Groq, pas OpenRouter.
        Assert.Contains("api.groq.com", fake.LastRequest!.RequestUri!.ToString());
        Assert.Equal("gsk-groq", fake.LastRequest.Headers.Authorization!.Parameter);
        // Multipart avec le modèle sans préfixe provider.
        Assert.StartsWith("multipart/form-data", fake.LastRequest.Content!.Headers.ContentType!.MediaType);
        Assert.Contains("whisper-large-v3-turbo", fake.LastRequestBody);
        Assert.DoesNotContain("openai/whisper", fake.LastRequestBody);
    }

    [Fact]
    public async Task TranscribeAsync_TimeoutDepasse_LeveExceptionDelai()
    {
        // Réponse retardée bien au-delà du délai : le délai par appel doit couper proprement.
        var fake = new FakeHttpHandler(HttpStatusCode.OK, "{\"text\":\"ok\"}", delayMs: 10_000);
        var svc = new TranscriptionService(new HttpClient(fake));

        var ex = await Assert.ThrowsAsync<OpenRouterException>(() =>
            svc.TranscribeAsync(new byte[] { 1 }, "m", "k", null, null, CancellationToken.None,
                TimeSpan.FromMilliseconds(50)));
        Assert.Contains("Délai", ex.Message);
    }

    [Fact]
    public async Task TranscribeAsync_ReponseSansChampText_LeveExceptionReponseInattendue()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.OK, "{\"duration\":1.2}");
        var svc = new TranscriptionService(new HttpClient(fake));

        var ex = await Assert.ThrowsAsync<OpenRouterException>(() =>
            svc.TranscribeAsync(new byte[] { 1 }, "m", "k", null, null, CancellationToken.None));
        Assert.Contains("inattendue", ex.Message);
    }

    [Fact]
    public async Task TranscribeAsync_AnnulationUtilisateur_NEstPasUnTimeout()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.OK, "{\"text\":\"ok\"}", delayMs: 10_000);
        var svc = new TranscriptionService(new HttpClient(fake));
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // l'utilisateur annule (Échap)

        // Doit remonter une annulation, PAS une OpenRouterException « Délai ».
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            svc.TranscribeAsync(new byte[] { 1 }, "m", "k", null, null, cts.Token, TimeSpan.FromSeconds(30)));
    }

    [Fact]
    public async Task TranscribeAsync_ModeleDisparu404_GuideVersLesReglages()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.NotFound, "{\"error\":{\"message\":\"model_decommissioned\"}}");
        var svc = new TranscriptionService(new HttpClient(fake));

        var ex = await Assert.ThrowsAsync<OpenRouterException>(() =>
            svc.TranscribeAsync(new byte[] { 1 }, "m", "k", null, null, CancellationToken.None));
        Assert.Contains("404", ex.Message);
        Assert.Contains("Réglages", ex.Message);
    }

    [Fact]
    public async Task TranscribeAsync_Erreur500_SansMentionDesReglages()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.InternalServerError, "{}");
        var svc = new TranscriptionService(new HttpClient(fake));

        var ex = await Assert.ThrowsAsync<OpenRouterException>(() =>
            svc.TranscribeAsync(new byte[] { 1 }, "m", "k", null, null, CancellationToken.None));
        Assert.Contains("500", ex.Message);
        Assert.DoesNotContain("Réglages", ex.Message);
    }

    // --- TimeoutFor (correctif audit 2026-07-16 : dictée longue sur lien lent) ---

    [Fact]
    public void TimeoutFor_PetitAudio_ResteAuPlancherDe45s()
        => Assert.Equal(TimeSpan.FromSeconds(45), TranscriptionService.TimeoutFor(0));

    [Fact]
    public void TimeoutFor_DicteeDe5Minutes_MonteProportionnellement()
        // 10 Mio → 45 + 10 × 10 = 145 s (une dictée de 300 s fait ~9,6 Mo de WAV 16 kHz mono 16 bits).
        => Assert.Equal(TimeSpan.FromSeconds(145), TranscriptionService.TimeoutFor(10 * 1_048_576));

    [Fact]
    public void TimeoutFor_EstPlafonneA180s()
        => Assert.Equal(TimeSpan.FromSeconds(180), TranscriptionService.TimeoutFor(50 * 1_048_576));
}
