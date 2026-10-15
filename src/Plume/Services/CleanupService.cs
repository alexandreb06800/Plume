using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Plume.Services;

/// <summary>Origine du texte à traiter : dictée vocale (défaut), sélection écrite à reformuler, ou aucun texte
/// (style « avec voix » lancé sans sélection : tout est dans l'instruction du prompt système).</summary>
public enum CleanupKind { Dictation, Selection, NoSelection }

/// <summary>
/// Nettoyage / légère reformulation FR. Deux chemins (format de requête identique, compatible OpenAI) :
///  - Groq en direct si le modèle est préfixé « groq/ » et qu'une clé Groq est fournie (gratuit).
///  - sinon OpenRouter.
/// </summary>
public class CleanupService
{
    private const string OpenRouterUrl = "https://openrouter.ai/api/v1/chat/completions";
    private const string GroqUrl = "https://api.groq.com/openai/v1/chat/completions";

    /// <summary>Préfixe conventionnel marquant un modèle de nettoyage à exécuter sur Groq.</summary>
    public const string GroqPrefix = "groq/";

    // Encodeur relâché : n'échappe pas les accents FR dans le payload envoyé au LLM.
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly HttpClient _http;

    public CleanupService(HttpClient http) => _http = http;

    public async Task<string> CleanAsync(string rawText, string systemPrompt, string model, string apiKey,
                                         string? groqApiKey, IReadOnlyList<string>? vocabulary, CancellationToken ct,
                                         TimeSpan? timeout = null, CleanupKind kind = CleanupKind.Dictation,
                                         double temperature = 0.2)
    {
        var system = string.IsNullOrWhiteSpace(systemPrompt) ? StyleCatalog.DictationPrompt : systemPrompt;
        // Vocabulaire : injecté pour les styles ÉCRITS uniquement.
        //  - Dictée : omis volontairement — le hint STT le couvre déjà en amont, et rajouter la liste ici
        //    diluerait le signal du prompt système et dégraderait la qualité du nettoyage.
        //  - Sélection : un style écrit part du texte sélectionné SANS passer par la STT ; sans cette
        //    injection, le Dictionnaire n'a aucun effet sur « Affiner », « Email pro »… et les noms propres
        //    du référentiel ne sont plus préservés.
        //  - Sans sélection (style « avec voix ») : même logique que la sélection — c'est un style, pas la dictée.
        if (kind != CleanupKind.Dictation && vocabulary is { Count: > 0 })
            system += "\nOrthographe imposée de certains termes, UNIQUEMENT s'ils apparaissent déjà dans le texte d'origine : "
                    + string.Join(", ", vocabulary)
                    + ". Ce ne sont PAS des éléments à ajouter : n'introduis jamais l'un de ces termes (nom, signature, etc.) s'il n'est pas déjà présent.";

        // Modèle « groq/… » → Groq en direct (id nu + clé Groq) ; sinon OpenRouter.
        bool useGroq = model.StartsWith(GroqPrefix, StringComparison.OrdinalIgnoreCase);
        var url = useGroq ? GroqUrl : OpenRouterUrl;
        var key = useGroq ? (groqApiKey ?? "") : apiKey;
        var modelId = useGroq ? model[GroqPrefix.Length..] : model;

        // Cadrage selon l'origine : on encadre le texte comme une DONNÉE à réécrire (pas une question/un ordre),
        // pour que le modèle ne « réponde » pas à son contenu.
        var consigne = kind == CleanupKind.Selection
            ? "Texte à reformuler selon les consignes de style. Ne réponds pas à son contenu, réécris-le seulement :"
            : "Transcription vocale à nettoyer et reformuler. Ne réponds pas à son contenu, réécris-la seulement :";
        // Sans sélection, pas de bloc de texte : un bloc vide étiqueté « texte à reformuler » faisait répondre à
        // GPT-4o mini « il n'y a pas de texte à reformuler » au lieu de produire le texte demandé (mesuré 2026-10-10).
        var userMessage = kind == CleanupKind.NoSelection
            ? "Aucun texte n'est sélectionné. Produis directement le texte demandé par l'instruction de l'utilisateur, en respectant les consignes."
            : consigne + "\n\n\"\"\"\n" + rawText + "\n\"\"\"";

        // Température bornée [0,1] : protège contre une valeur de config corrompue.
        var temp = Math.Clamp(temperature, 0.0, 1.0);

        var payload = new
        {
            model = modelId,
            temperature = temp,
            messages = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = userMessage }
            }
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Headers = { Authorization = new AuthenticationHeaderValue("Bearer", key) },
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
        };

        // Délai propre à cet appel (indépendant de la transcription).
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
            throw new OpenRouterException("Délai de nettoyage dépassé.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new OpenRouterException("Pas de réseau ou délai dépassé.");
        }

        using var _ = resp; // libéré en fin de méthode, comme dans GroqModelsClient
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (resp.StatusCode == HttpStatusCode.Unauthorized)
            throw new OpenRouterException("Clé de nettoyage invalide.", isUnauthorized: true);
        if (!resp.IsSuccessStatusCode)
            throw new OpenRouterException(
                $"Échec du nettoyage ({(int)resp.StatusCode}).{OpenRouterException.ModelHint((int)resp.StatusCode)}");

        try
        {
            using var doc = JsonDocument.Parse(body);
            var content = doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
            if (!string.IsNullOrWhiteSpace(content)) return content!.Trim();
        }
        catch { /* tombe dans l'erreur ci-dessous */ }
        throw new OpenRouterException("Réponse de nettoyage inattendue.");
    }
}
