using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Plume.Services;

/// <summary>
/// Transcription audio. Deux chemins :
///  - Groq en direct (<c>/openai/v1/audio/transcriptions</c>) si une clé Groq est fournie : nettement plus rapide.
///  - sinon OpenRouter (<c>/audio/transcriptions</c>) en repli.
/// </summary>
public class TranscriptionService
{
    private const string OpenRouterUrl = "https://openrouter.ai/api/v1/audio/transcriptions";
    private const string GroqUrl = "https://api.groq.com/openai/v1/audio/transcriptions";
    private readonly HttpClient _http;

    public TranscriptionService(HttpClient http) => _http = http;

    /// <summary>Délai STT adapté à la taille de l'audio : 45 s de base + 10 s par Mio (l'upload domine
    /// sur un lien lent — VPN, 4G), plafonné à 180 s. Une dictée de 5 min (~9,6 Mo) obtient ~2 min 15 là
    /// où le délai fixe de 45 s la perdait entièrement. Pur et testable.</summary>
    public static TimeSpan TimeoutFor(int wavByteCount)
    {
        var seconds = 45.0 + wavByteCount / 1_048_576.0 * 10.0;
        return TimeSpan.FromSeconds(Math.Min(180.0, seconds));
    }

    public async Task<string> TranscribeAsync(byte[] wav, string model, string apiKey, string? groqApiKey,
                                              IReadOnlyList<string>? vocabulary, CancellationToken ct,
                                              TimeSpan? timeout = null)
    {
        var hint = vocabulary is { Count: > 0 }
            ? "Vocabulaire attendu : " + string.Join(", ", vocabulary)
            : null;

        using var req = string.IsNullOrWhiteSpace(groqApiKey)
            ? BuildOpenRouterRequest(wav, model, apiKey, hint)
            : BuildGroqRequest(wav, model, groqApiKey!, hint);

        // Délai propre à cet appel : un timeout STT ne doit pas manger le budget du nettoyage.
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (timeout is { } t) timeoutCts.CancelAfter(t);

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(req, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Annulation déclenchée par notre délai (et non par l'utilisateur via Échap).
            throw new OpenRouterException("Délai de transcription dépassé. Réessaie ou raccourcis ta dictée.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new OpenRouterException("Pas de réseau ou délai dépassé.");
        }

        using var _ = resp; // libéré en fin de méthode, comme dans GroqModelsClient
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
            throw new OpenRouterException("Clé de transcription invalide.", isUnauthorized: true);
        if (!resp.IsSuccessStatusCode)
            throw new OpenRouterException(
                $"Échec de la transcription ({(int)resp.StatusCode}).{OpenRouterException.ModelHint((int)resp.StatusCode)}");

        return ExtractText(body);
    }

    /// <summary>Requête Groq : multipart/form-data (fichier audio + champs), modèle sans préfixe provider.</summary>
    private static HttpRequestMessage BuildGroqRequest(byte[] wav, string model, string groqApiKey, string? hint)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(wav);
        file.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(file, "file", "audio.wav");
        form.Add(new StringContent(GroqModelId(model)), "model");
        form.Add(new StringContent("fr"), "language");
        form.Add(new StringContent("json"), "response_format");
        if (hint is not null) form.Add(new StringContent(hint), "prompt");

        return new HttpRequestMessage(HttpMethod.Post, GroqUrl)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", groqApiKey) },
            Content = form
        };
    }

    /// <summary>Requête OpenRouter : JSON avec audio base64 (champ "input_audio") + épinglage provider rapide.</summary>
    private static HttpRequestMessage BuildOpenRouterRequest(byte[] wav, string model, string apiKey, string? hint)
    {
        // OpenRouter attend "input_audio" (objet base64 + format), pas "audio".
        var audio = new { data = Convert.ToBase64String(wav), format = "wav" };
        object payload = hint is not null
            ? new { model, language = "fr", input_audio = audio,
                    provider = new { order = new[] { "Groq" }, allow_fallbacks = true, groq = new { prompt = hint } } }
            : new { model, language = "fr", input_audio = audio,
                    provider = new { order = new[] { "Groq" }, allow_fallbacks = true } };

        return new HttpRequestMessage(HttpMethod.Post, OpenRouterUrl)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", apiKey) },
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
    }

    /// <summary>Groq attend l'id nu (ex. "whisper-large-v3-turbo"), sans préfixe provider "openai/".</summary>
    private static string GroqModelId(string model)
    {
        var slash = model.LastIndexOf('/');
        return slash >= 0 ? model[(slash + 1)..] : model;
    }

    /// <summary>Parsing défensif : champ "text" à la racine.</summary>
    private static string ExtractText(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                return t.GetString()!.Trim();
        }
        catch { /* tombe dans l'erreur ci-dessous */ }
        throw new OpenRouterException("Réponse de transcription inattendue.");
    }
}
