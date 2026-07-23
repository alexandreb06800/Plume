namespace Plume.Services;

/// <summary>Erreur d'appel OpenRouter avec drapeau 401 pour ouvrir les réglages.</summary>
public class OpenRouterException : Exception
{
    public bool IsUnauthorized { get; }
    public OpenRouterException(string message, bool isUnauthorized = false) : base(message)
        => IsUnauthorized = isUnauthorized;

    /// <summary>Complément de message (FR) pour un statut HTTP typique d'un modèle retiré par le
    /// fournisseur (400/404/410) — inévitable sur plusieurs années sans mise à jour de l'app.
    /// Guide vers les Réglages, où le modèle se corrige sans réinstallation (saisie libre d'un id
    /// + bouton « Vérifier les modèles »). Chaîne vide pour tout autre statut.</summary>
    public static string ModelHint(int statusCode)
        => statusCode is 400 or 404 or 410
            ? " Le modèle configuré n'existe peut-être plus : ouvre les Réglages → Général et vérifie les modèles."
            : "";
}
