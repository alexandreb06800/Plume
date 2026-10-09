namespace Plume.Services;

/// <summary>Décide quels hôtes préchauffer avant une dictée, selon le routage statique réel
/// (voir TranscriptionService/CleanupService/AgentChatService) : on ne préchauffe que ce qui servira.
/// Pur et testable.</summary>
public static class WarmUpPlanner
{
    /// <summary>Endpoint Groq léger (liste des modèles, quelques Ko) : ouvre DNS + TLS + pool.</summary>
    public const string GroqUrl = "https://api.groq.com/openai/v1/models";

    /// <summary>Endpoint OpenRouter léger (infos de la clé, quelques centaines d'octets). Surtout pas
    /// <c>/models</c> : ~765 Ko non compressés, téléchargés puis jetés à chaque début d'écoute.</summary>
    public const string OpenRouterUrl = "https://openrouter.ai/api/v1/key";

    /// <summary>(groq, openRouter) : vrai si l'hôte sert à au moins un flux ET que sa clé est présente.
    /// STT : Groq si clé Groq, sinon OpenRouter. Nettoyage et assistant : Groq si le modèle est
    /// préfixé « groq/ », sinon OpenRouter.</summary>
    public static (bool Groq, bool OpenRouter) Targets(bool hasGroqKey, bool hasOpenRouterKey,
                                                       string cleanupModel, bool agentEnabled, string? agentModel)
    {
        static bool OnGroq(string m) => m.StartsWith(CleanupService.GroqPrefix, StringComparison.OrdinalIgnoreCase);

        var agent = string.IsNullOrWhiteSpace(agentModel) ? cleanupModel : agentModel!;
        bool sttOnGroq = hasGroqKey;
        bool needGroq = sttOnGroq || OnGroq(cleanupModel) || (agentEnabled && OnGroq(agent));
        bool needOpenRouter = !sttOnGroq || !OnGroq(cleanupModel) || (agentEnabled && !OnGroq(agent));
        return (needGroq && hasGroqKey, needOpenRouter && hasOpenRouterKey);
    }
}
