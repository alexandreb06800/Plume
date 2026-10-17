using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Plume.Models;

namespace Plume.Services;

/// <summary>
/// Assistant vocal (V4-C) : pose une question (avec l'historique de la conversation) et renvoie une réponse.
/// Ne réécrit rien, n'insère rien dans le document. Deux chemins (format OpenAI identique) :
///  - Groq en direct si le modèle est préfixé « groq/ » et qu'une clé Groq est fournie ;
///  - sinon OpenRouter.
/// Le plumbing HTTP/routage est volontairement autonome (pas de client partagé avec CleanupService)
/// pour ne pas toucher au chemin critique dictée/nettoyage.
/// </summary>
public class AgentChatService
{
    private const string OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";
    private const string GroqUrl = "https://api.groq.com/openai/v1/chat/completions";
    public const string GroqPrefix = "groq/";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull // reasoning_effort absent si null
    };
    private static readonly CultureInfo Fr = new("fr-FR");

    private readonly HttpClient _http;

    public AgentChatService(HttpClient http) => _http = http;

    /// <summary>Construit le tableau de messages OpenAI-compatible : system (avec date du jour) +
    /// historique (user/assistant par tour) + nouvelle question. Pur et testable.</summary>
    public static object[] BuildMessages(AgentSession session, string question, DateTime now,
                                         IReadOnlyList<string>? vocabulary)
    {
        var jour = now.ToString("dddd d MMMM yyyy", Fr); // ex. « lundi 29 juin 2026 »
        var heure = now.ToString("HH'h'mm", Fr);          // ex. « 14h32 »
        var system =
            "Tu es un assistant vocal intégré à Plume. Réponds en français, de façon concise et directe. "
            + "Si tu ne sais pas, dis-le simplement plutôt que d'inventer. "
            + $"Pour information, nous sommes le {jour} et il est {heure}.";
        if (vocabulary is { Count: > 0 })
            system += " Termes à connaître et orthographier ainsi : " + string.Join(", ", vocabulary) + ".";

        var messages = new List<object> { new { role = "system", content = system } };
        foreach (var t in session.Turns)
        {
            messages.Add(new { role = "user", content = t.Question });
            messages.Add(new { role = "assistant", content = t.Answer });
        }
        messages.Add(new { role = "user", content = question });
        return messages.ToArray();
    }

    public async Task<string> AskAsync(AgentSession session, string question, DateTime now, string model,
                                       string apiKey, string? groqApiKey, IReadOnlyList<string>? vocabulary,
                                       CancellationToken ct, TimeSpan? timeout = null)
    {
        bool useGroq = model.StartsWith(GroqPrefix, StringComparison.OrdinalIgnoreCase);
        var url = useGroq ? GroqUrl : OpenRouterUrl;
        var key = useGroq ? (groqApiKey ?? "") : apiKey;
        var modelId = useGroq ? model[GroqPrefix.Length..] : model;

        var payload = new
        {
            model = modelId,
            temperature = 0.3, // un assistant : un peu de souplesse, sans dériver
            reasoning_effort = useGroq ? ModelCatalog.Find(model)?.ReasoningEffort : null,
            messages = BuildMessages(session, question, now, vocabulary)
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", key) },
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout is { } t) timeoutCts.CancelAfter(t);

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(req, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new OpenRouterException("Délai de réponse dépassé.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new OpenRouterException("Pas de réseau ou délai dépassé.");
        }

        using var _ = resp; // libéré en fin de méthode, comme dans GroqModelsClient
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
            throw new OpenRouterException("Clé invalide.", isUnauthorized: true);
        if (!resp.IsSuccessStatusCode)
            throw new OpenRouterException(
                $"Échec de la requête ({(int)resp.StatusCode}).{OpenRouterException.StatusHint((int)resp.StatusCode)}");

        try
        {
            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            content = CleanupService.StripReasoning(content);
            if (!string.IsNullOrWhiteSpace(content)) return content!;
        }
        catch { /* tombe dans l'erreur ci-dessous */ }
        throw new OpenRouterException("Réponse inattendue.");
    }
}
