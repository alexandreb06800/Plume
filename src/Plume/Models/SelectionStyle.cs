namespace Plume.Models;

/// <summary>Style du menu de sélection : appliqué à un texte sélectionné via la pastille.
/// WithVoice = false → applique le prompt figé ; true → écoute une instruction dictée avant d'appliquer.</summary>
public class SelectionStyle
{
    public string Id { get; set; } = "";        // "corriger", "affiner"… pour les intégrés ; guid pour les persos
    public string Name { get; set; } = "";       // libellé affiché dans le menu et l'éditeur
    public string Prompt { get; set; } = "";      // system prompt appliqué à la sélection
    public bool WithVoice { get; set; }          // false = style écrit ; true = écoute une instruction dictée
    public bool IsFavorite { get; set; }         // favori → en tête du menu
    public bool ShowInMenu { get; set; } = true; // visible dans le menu (un intégré masqué n'apparaît plus)
    public bool IsBuiltIn { get; set; }          // intégré : injecté au premier lancement
    public double Temperature { get; set; } = 0.2; // créativité (0 = fidèle, 1 = libre)
    public string? Model { get; set; }           // modèle IA de ce style ; null/vide = modèle de nettoyage global (AppConfig.CleanupModel)
    public bool ResultInWindow { get; set; }     // true = résultat dans une fenêtre (Copier/Insérer) au lieu de remplacer la sélection
}

/// <summary>Position mémorisée de la pastille flottante (coordonnées écran). Null = coin par défaut.</summary>
public class BadgePosition
{
    public double X { get; set; }
    public double Y { get; set; }
}
