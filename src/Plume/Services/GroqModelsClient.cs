using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Plume.Services;

/// <summary>
/// Interroge l'API Groq pour la liste des modèles disponibles. Appel réseau UNIQUEMENT
/// à la demande (jamais au démarrage — RÈGLE PRIMORDIALE). Ne cible que Groq, le provider
/// réellement utilisé ; OpenRouter reste géré ailleurs.
/// </summary>
public static class GroqModelsClient
{
    private const string ModelsUrl = "https://api.groq.com/openai/v1/models";

    /// <summary>
    /// Récupère les ids de modèles publiés chez Groq. Lève <see cref="OpenRouterException"/>
    /// avec un message FR en cas d'échec (hors-ligne, clé invalide…), pour affichage sans crash.
    /// </summary>
    public static async Task<IReadOnlyList<string>> FetchModelIdsAsync(
        HttpClient http, string groqKey, CancellationToken ct, TimeSpan timeout)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        using var req = new HttpRequestMessage(HttpMethod.Get, ModelsUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", groqKey);

        HttpResponseMessage resp;
        try
        {
            resp = await http.SendAsync(req, cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new OpenRouterException("Délai dépassé : vérifie ta connexion.");
        }
        catch (HttpRequestException)
        {
            throw new OpenRouterException("Impossible de joindre Groq : vérifie ta connexion.");
        }

        using (resp)
        {
            if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new OpenRouterException("Clé Groq invalide.", isUnauthorized: true);
            if (!resp.IsSuccessStatusCode)
                throw new OpenRouterException($"Groq a répondu {(int)resp.StatusCode}.");

            var json = await resp.Content.ReadAsStringAsync(cts.Token);
            return ParseModelIds(json);
        }
    }

    /// <summary>Extrait les ids du JSON /models. Logique pure ; ne lève jamais (liste vide si invalide).</summary>
    public static IReadOnlyList<string> ParseModelIds(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            var ids = new List<string>();
            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                {
                    var value = id.GetString();
                    if (!string.IsNullOrWhiteSpace(value)) ids.Add(value);
                }
            }
            return ids;
        }
        catch (JsonException)
        {
            return Array.Empty<string>();
        }
    }
}
