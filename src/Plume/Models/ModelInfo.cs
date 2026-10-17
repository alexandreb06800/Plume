namespace Plume.Models;

/// <summary>Type de modèle : transcription (STT) ou nettoyage (LLM).</summary>
public enum ModelKind { Stt, Cleanup }

/// <summary>Entrée curatée du catalogue de modèles (prix et fiabilité écrits à la main).</summary>
public class ModelInfo
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public ModelKind Kind { get; init; }
    public string PriceLabel { get; init; } = "";            // ex. « 0,04 €/1M » ou « in 0,14 · out 0,55 €/1M »
    public string CostPerDictationLabel { get; init; } = ""; // ex. « ≈ 0,002 € »
    public int Reliability { get; init; }                    // 1..5
    /// <summary>Réglage de raisonnement envoyé tel quel (« low », « medium »…) aux modèles qui l'acceptent ;
    /// null = paramètre non envoyé (comportement par défaut du modèle).</summary>
    public string? ReasoningEffort { get; init; }
}
