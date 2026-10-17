using Plume.Models;

namespace Plume.Services;

/// <summary>
/// Catalogue curaté de modèles recommandés (transcription + nettoyage).
/// Prix et notes de fiabilité écrits à la main : zéro appel réseau.
/// À réviser manuellement avant chaque release (tarifs OpenRouter du moment).
/// </summary>
public static class ModelCatalog
{
    public static IReadOnlyList<ModelInfo> Stt { get; } = new[]
    {
        new ModelInfo
        {
            Id = "openai/whisper-large-v3-turbo", DisplayName = "Whisper Large v3 Turbo",
            Kind = ModelKind.Stt, PriceLabel = "0,04 €/1M",
            CostPerDictationLabel = "≈ 0,002 €", Reliability = 4
        },
        new ModelInfo
        {
            Id = "openai/whisper-large-v3", DisplayName = "Whisper Large v3",
            Kind = ModelKind.Stt, PriceLabel = "0,06 €/1M",
            CostPerDictationLabel = "≈ 0,003 €", Reliability = 5
        },
    };

    public static IReadOnlyList<ModelInfo> Cleanup { get; } = new[]
    {
        // Modèles Groq (préfixe « groq/ ») : exécutés sur Groq en direct, gratuits. Nécessitent une clé Groq.
        // Le premier modèle Groq de la liste est celui que la prise en main retient sans clé OpenRouter
        // (ConfigService.CleanupModelUtilisable). Révisé le 2026-10-09 : Llama 3.3 70B et 3.1 8B arrêtés par
        // Groq le 16/08/2026 ; comparatif sur le prompt de dictée réel (Qwen 3.8 : meilleur et ~250 ms ;
        // GPT-OSS 120B : ~900 ms, rate une auto-correction « non pardon »). Groq renouvelle souvent ses Qwen.
        new ModelInfo
        {
            Id = "groq/qwen/qwen3.8-27b", DisplayName = "Qwen 3.8 27B (Groq)",
            Kind = ModelKind.Cleanup, PriceLabel = "Gratuit (Groq)",
            CostPerDictationLabel = "Gratuit · ultra-rapide", Reliability = 5
        },
        new ModelInfo
        {
            Id = "groq/openai/gpt-oss-120b", DisplayName = "GPT-OSS 120B (Groq)",
            Kind = ModelKind.Cleanup, PriceLabel = "Gratuit (Groq)",
            CostPerDictationLabel = "Gratuit", Reliability = 3,
            ReasoningEffort = "low" // raisonnement court : réponse plus rapide (mesuré au lot 2 de l'audit 2026-10-16)
        },
        new ModelInfo
        {
            Id = "openai/gpt-4o-mini", DisplayName = "GPT-4o mini",
            Kind = ModelKind.Cleanup, PriceLabel = "in 0,14 · out 0,55 €/1M",
            CostPerDictationLabel = "≈ 0,001 €", Reliability = 4
        },
    };

    /// <summary>Entrée correspondant à l'id, ou null si inconnu (= modèle personnalisé).</summary>
    public static ModelInfo? Find(string id)
        => Stt.Concat(Cleanup).FirstOrDefault(m => m.Id == id);
}
