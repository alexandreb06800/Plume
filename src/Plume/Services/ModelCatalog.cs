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
        new ModelInfo
        {
            Id = "groq/llama-3.3-70b-versatile", DisplayName = "Llama 3.3 70B (Groq)",
            Kind = ModelKind.Cleanup, PriceLabel = "Gratuit (Groq)",
            CostPerDictationLabel = "Gratuit", Reliability = 4
        },
        new ModelInfo
        {
            Id = "groq/llama-3.1-8b-instant", DisplayName = "Llama 3.1 8B (Groq)",
            Kind = ModelKind.Cleanup, PriceLabel = "Gratuit (Groq)",
            CostPerDictationLabel = "Gratuit · ultra-rapide", Reliability = 3
        },
        new ModelInfo
        {
            Id = "openai/gpt-4o-mini", DisplayName = "GPT-4o mini",
            Kind = ModelKind.Cleanup, PriceLabel = "in 0,14 · out 0,55 €/1M",
            CostPerDictationLabel = "≈ 0,001 €", Reliability = 4
        },
        new ModelInfo
        {
            Id = "google/gemini-2.0-flash-001", DisplayName = "Gemini 2.0 Flash",
            Kind = ModelKind.Cleanup, PriceLabel = "in 0,10 · out 0,40 €/1M",
            CostPerDictationLabel = "≈ 0,001 €", Reliability = 4
        },
    };

    /// <summary>Entrée correspondant à l'id, ou null si inconnu (= modèle personnalisé).</summary>
    public static ModelInfo? Find(string id)
        => Stt.Concat(Cleanup).FirstOrDefault(m => m.Id == id);
}
