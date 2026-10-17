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
        // (ModelCatalog.DefaultGroqCleanupId). Décision du 2026-10-16 : GPT-OSS 120B, classé « Production » chez
        // Groq ; Qwen 3.8 est en « Preview » (retrait possible sans préavis : qwen3-32b et qwen3.6-27b l'ont déjà
        // été). Comparatif du 2026-10-09 sur le prompt de dictée réel : Qwen 3.8 meilleur et ~250 ms ;
        // GPT-OSS 120B ~900 ms, rate une auto-correction « non pardon ».
        new ModelInfo
        {
            Id = "groq/openai/gpt-oss-120b", DisplayName = "GPT-OSS 120B (Groq)",
            Kind = ModelKind.Cleanup, PriceLabel = "Gratuit (Groq)",
            CostPerDictationLabel = "Gratuit", Reliability = 3,
            ReasoningEffort = "low" // raisonnement court : réponse plus rapide (lot 2 de l'audit 2026-10-16)
        },
        new ModelInfo
        {
            Id = "groq/qwen/qwen3.8-27b", DisplayName = "Qwen 3.8 27B (Groq, préversion)",
            Kind = ModelKind.Cleanup, PriceLabel = "Gratuit (Groq)",
            CostPerDictationLabel = "Gratuit · ultra-rapide", Reliability = 5
        },
        new ModelInfo
        {
            Id = "openai/gpt-4o-mini", DisplayName = "GPT-4o mini",
            Kind = ModelKind.Cleanup, PriceLabel = "in 0,14 · out 0,55 €/1M",
            CostPerDictationLabel = "≈ 0,001 €", Reliability = 4
        },
    };

    /// <summary>Premier modèle Groq du catalogue : défaut de la prise en main sans clé OpenRouter.</summary>
    public static string DefaultGroqCleanupId { get; } = Cleanup.First(
        m => m.Id.StartsWith(CleanupService.GroqPrefix, StringComparison.OrdinalIgnoreCase)).Id;

    // Modèles retirés par leur fournisseur → remplaçant. Sources : Groq, page « Deprecations »
    // (https://console.groq.com/docs/deprecations) ; Gemini 2.0 Flash constaté en 404 le 2026-10-09.
    // Ids écrits en dur (pas calculés) : un remplaçant ne doit jamais changer sans relecture.
    private static readonly Dictionary<string, string> Replacements = new(StringComparer.OrdinalIgnoreCase)
    {
        ["groq/llama-3.3-70b-versatile"] = "groq/openai/gpt-oss-120b",
        ["groq/llama-3.1-8b-instant"] = "groq/openai/gpt-oss-120b",
        ["groq/qwen/qwen3-32b"] = "groq/openai/gpt-oss-120b",
        ["groq/qwen/qwen3.6-27b"] = "groq/qwen/qwen3.8-27b",
        ["google/gemini-2.0-flash-001"] = "openai/gpt-4o-mini",
    };

    /// <summary>Remplaçant d'un modèle retiré par son fournisseur ; null si le modèle est actif ou inconnu
    /// (un modèle personnalisé n'est jamais touché).</summary>
    public static string? ReplacementFor(string? id)
        => id is not null && Replacements.TryGetValue(id, out var r) ? r : null;

    /// <summary>Entrée correspondant à l'id, ou null si inconnu (= modèle personnalisé).</summary>
    public static ModelInfo? Find(string id)
        => Stt.Concat(Cleanup).FirstOrDefault(m => m.Id == id);
}
