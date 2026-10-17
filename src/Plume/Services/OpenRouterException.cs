namespace Plume.Services;

/// <summary>Erreur d'appel OpenRouter avec drapeau 401 pour ouvrir les réglages.</summary>
public class OpenRouterException : Exception
{
    public bool IsUnauthorized { get; }
    public OpenRouterException(string message, bool isUnauthorized = false) : base(message)
        => IsUnauthorized = isUnauthorized;

    /// <summary>Complément de message (FR) pour un statut HTTP parlant : 400/404/410 = modèle retiré par le
    /// fournisseur (inévitable sur plusieurs années sans mise à jour de l'app), guidé vers les Réglages où le
    /// modèle se corrige sans réinstallation (saisie libre d'un id + bouton « Vérifier les modèles ») ;
    /// 429 = limite de requêtes. Chaîne vide pour tout autre statut.</summary>
    public static string StatusHint(int statusCode) => statusCode switch
    {
        400 or 404 or 410 => " Le modèle configuré n'existe peut-être plus : ouvre les Réglages → Général et vérifie les modèles.",
        429 => " Trop de requêtes pour le moment (limite du fournisseur, fréquente avec une clé gratuite) : réessaie dans une minute.",
        _ => "",
    };

    /// <summary>Bulle affichée quand le nettoyage échoue mais que le texte brut a été inséré quand même.</summary>
    public static string RawTextFallbackMessage(OpenRouterException ex)
        => "Nettoyage indisponible, texte brut inséré. " + ex.Message;
}
