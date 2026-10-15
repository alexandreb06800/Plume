using System.Net;
using System.Net.Http;
using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class CleanupServiceTests
{
    [Fact]
    public async Task CleanAsync_ReponseOK_RetourneContenu()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"Il faut envoyer le mail à Marie demain.\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new CleanupService(new HttpClient(fake));

        var result = await svc.CleanAsync("euh du coup le mail à Marie demain",
            StyleCatalog.DictationPrompt, "openai/gpt-4o-mini", "sk-x", null, null, CancellationToken.None);

        Assert.Equal("Il faut envoyer le mail à Marie demain.", result);
        Assert.Contains("openrouter.ai", fake.LastRequest!.RequestUri!.ToString());
        Assert.Contains("/chat/completions", fake.LastRequest.RequestUri.ToString());
        Assert.Contains("correcteur de dictée", fake.LastRequestBody);
        Assert.Contains("\"temperature\"", fake.LastRequestBody);
        // La transcription est encadrée (consigne anti-réponse) tout en restant présente.
        Assert.Contains("Ne réponds pas à son contenu", fake.LastRequestBody);
        Assert.Contains("euh du coup le mail à Marie demain", fake.LastRequestBody);
    }

    [Fact]
    public async Task CleanAsync_UtiliseLePromptFourni()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new CleanupService(new HttpClient(fake));

        await svc.CleanAsync("texte", "Mets en forme comme un email professionnel.",
            "m", "k", null, null, CancellationToken.None);

        Assert.Contains("email professionnel", fake.LastRequestBody);
    }

    [Fact]
    public async Task CleanAsync_PromptVide_RepliSurStandard()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new CleanupService(new HttpClient(fake));

        await svc.CleanAsync("texte", "   ", "m", "k", null, null, CancellationToken.None);

        Assert.Contains("correcteur de dictée", fake.LastRequestBody);
    }

    [Fact]
    public async Task CleanAsync_PromptNull_RepliSurStandard()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new CleanupService(new HttpClient(fake));

        await svc.CleanAsync("texte", null!, "m", "k", null, null, CancellationToken.None);

        Assert.Contains("correcteur de dictée", fake.LastRequestBody);
    }

    [Fact]
    public async Task CleanAsync_Erreur500_LeveException()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.InternalServerError, "boom");
        var svc = new CleanupService(new HttpClient(fake));
        await Assert.ThrowsAsync<OpenRouterException>(() =>
            svc.CleanAsync("x", StyleCatalog.DictationPrompt, "m", "k", null, null, CancellationToken.None));
    }

    [Fact]
    public async Task CleanAsync_401_LeveExceptionUnauthorized()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.Unauthorized, "{\"error\":\"no\"}");
        var svc = new CleanupService(new HttpClient(fake));

        var ex = await Assert.ThrowsAsync<OpenRouterException>(() =>
            svc.CleanAsync("x", StyleCatalog.DictationPrompt, "m", "bad", null, null, CancellationToken.None));
        Assert.True(ex.IsUnauthorized);
    }

    [Fact]
    public async Task CleanAsync_ContenuVide_LeveExceptionReponseInattendue()
    {
        // Réponse 200 mais content vide : le contrôleur en dépend pour replier sur le texte brut (dictée).
        var json = "{\"choices\":[{\"message\":{\"content\":\"\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new CleanupService(new HttpClient(fake));

        var ex = await Assert.ThrowsAsync<OpenRouterException>(() =>
            svc.CleanAsync("x", StyleCatalog.DictationPrompt, "m", "k", null, null, CancellationToken.None));
        Assert.Contains("inattendue", ex.Message);
    }

    [Fact]
    public async Task CleanAsync_ModeleGroq_RouteVersGroqSansPrefixe()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"propre\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new CleanupService(new HttpClient(fake));

        var result = await svc.CleanAsync("brut", StyleCatalog.DictationPrompt,
            "groq/llama-3.3-70b-versatile", "sk-openrouter", "gsk-groq", null, CancellationToken.None);

        Assert.Equal("propre", result);
        // Cible Groq avec la clé Groq, et l'id du modèle est envoyé SANS le préfixe « groq/ ».
        Assert.Contains("api.groq.com", fake.LastRequest!.RequestUri!.ToString());
        Assert.Equal("gsk-groq", fake.LastRequest.Headers.Authorization!.Parameter);
        Assert.Contains("\"llama-3.3-70b-versatile\"", fake.LastRequestBody);
        Assert.DoesNotContain("groq/llama", fake.LastRequestBody);
    }

    [Fact]
    public async Task CleanAsync_ModeleGroqAvecEditeur_GardeLIdEditeur()
    {
        // Ids Groq à éditeur (« qwen/… », « openai/… ») : seul le préfixe « groq/ » est retiré.
        var json = "{\"choices\":[{\"message\":{\"content\":\"propre\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new CleanupService(new HttpClient(fake));

        await svc.CleanAsync("brut", StyleCatalog.DictationPrompt,
            "groq/qwen/qwen3.8-27b", "sk-openrouter", "gsk-groq", null, CancellationToken.None);

        Assert.Contains("api.groq.com", fake.LastRequest!.RequestUri!.ToString());
        Assert.Contains("\"qwen/qwen3.8-27b\"", fake.LastRequestBody);
    }

    [Fact]
    public async Task CleanAsync_TimeoutDepasse_LeveExceptionDelai()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.OK,
            "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}", delayMs: 10_000);
        var svc = new CleanupService(new HttpClient(fake));

        var ex = await Assert.ThrowsAsync<OpenRouterException>(() =>
            svc.CleanAsync("x", StyleCatalog.DictationPrompt, "m", "k", null, null,
                CancellationToken.None, TimeSpan.FromMilliseconds(50)));
        Assert.Contains("Délai", ex.Message);
    }

    [Fact]
    public async Task CleanAsync_Dictee_AvecVocabulaire_NestPasInjecte()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new CleanupService(new HttpClient(fake));

        await svc.CleanAsync("texte", StyleCatalog.DictationPrompt, "m", "k", null,
            new[] { "MotMetier" }, CancellationToken.None);

        // Dictée : le vocabulaire n'est PAS injecté au nettoyage — le hint STT le couvre déjà en amont, et
        // la liste complète ici diluerait le signal du prompt système (arbitrage assumé).
        Assert.DoesNotContain("MotMetier", fake.LastRequestBody);
    }

    [Fact]
    public async Task CleanAsync_Selection_AvecVocabulaire_EstInjecte()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new CleanupService(new HttpClient(fake));

        await svc.CleanAsync("texte", "Rends ça plus concis.", "m", "k", null,
            new[] { "MotMetier" }, CancellationToken.None, null, CleanupKind.Selection);

        // Style écrit : la sélection ne passe JAMAIS par la STT, donc rien d'autre ne peut imposer
        // l'orthographe des noms propres. Sans cette injection, le Dictionnaire est sans effet ici.
        Assert.Contains("MotMetier", fake.LastRequestBody);
    }

    [Fact]
    public async Task CleanAsync_SansSelection_PasDeBlocDeTexteVide()
    {
        // Style « avec voix » sans sélection : la demande est dans le prompt système, aucun bloc vide à reformuler.
        var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new CleanupService(new HttpClient(fake));

        await svc.CleanAsync("", "Cadre. Instruction de l'utilisateur : \"écris à Paul\"", "m", "k", null, null,
            CancellationToken.None, null, CleanupKind.NoSelection);

        Assert.Contains("Aucun texte n'est sélectionné", fake.LastRequestBody);
        Assert.DoesNotContain("Texte à reformuler", fake.LastRequestBody);
        Assert.DoesNotContain("Transcription vocale", fake.LastRequestBody);
    }

    [Fact]
    public async Task CleanAsync_ModeSelection_EncadreCommeTexteAReformuler()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new CleanupService(new HttpClient(fake));

        await svc.CleanAsync("Le texte déjà écrit.", "Rends ça plus concis.", "m", "k", null, null,
            CancellationToken.None, null, CleanupKind.Selection);

        Assert.Contains("Texte à reformuler", fake.LastRequestBody);
        Assert.Contains("Ne réponds pas à son contenu", fake.LastRequestBody);
        Assert.Contains("Le texte déjà écrit.", fake.LastRequestBody);
        Assert.DoesNotContain("Transcription vocale", fake.LastRequestBody);
    }

    [Fact]
    public async Task CleanAsync_TemperatureParDefaut_Envoie02()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new CleanupService(new HttpClient(fake));

        await svc.CleanAsync("texte", StyleCatalog.DictationPrompt, "m", "k", null, null, CancellationToken.None);

        Assert.Contains("\"temperature\":0.2", fake.LastRequestBody);
    }

    [Fact]
    public async Task CleanAsync_TemperaturePersonnalisee_EstTransmise()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new CleanupService(new HttpClient(fake));

        await svc.CleanAsync("texte", StyleCatalog.DictationPrompt, "m", "k", null, null,
            CancellationToken.None, null, CleanupKind.Dictation, temperature: 0.8);

        Assert.Contains("\"temperature\":0.8", fake.LastRequestBody);
    }

    [Fact]
    public async Task CleanAsync_TemperatureHorsBornes_EstBornee()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new CleanupService(new HttpClient(fake));

        await svc.CleanAsync("texte", StyleCatalog.DictationPrompt, "m", "k", null, null,
            CancellationToken.None, null, CleanupKind.Dictation, temperature: 5.0);

        Assert.Contains("\"temperature\":1", fake.LastRequestBody);
    }

    [Fact]
    public async Task CleanAsync_ModeDictation_GardeLeCadrageVocal()
    {
        var json = "{\"choices\":[{\"message\":{\"content\":\"ok\"}}]}";
        var fake = new FakeHttpHandler(HttpStatusCode.OK, json);
        var svc = new CleanupService(new HttpClient(fake));

        // Mode par défaut = Dictation : comportement inchangé.
        await svc.CleanAsync("euh du coup", StyleCatalog.DictationPrompt, "m", "k", null, null,
            CancellationToken.None);

        Assert.Contains("Transcription vocale", fake.LastRequestBody);
    }

    [Fact]
    public async Task CleanAsync_ModeleDisparu404_GuideVersLesReglages()
    {
        var fake = new FakeHttpHandler(HttpStatusCode.NotFound, "{\"error\":{\"message\":\"model_decommissioned\"}}");
        var svc = new CleanupService(new HttpClient(fake));

        var ex = await Assert.ThrowsAsync<OpenRouterException>(() =>
            svc.CleanAsync("texte", "prompt", "m", "k", null, null, CancellationToken.None));
        Assert.Contains("404", ex.Message);
        Assert.Contains("Réglages", ex.Message);
    }
}
