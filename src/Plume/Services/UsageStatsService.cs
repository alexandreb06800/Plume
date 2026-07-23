namespace Plume.Services;

/// <summary>Logique pure des statistiques d'usage : comptage, temps gagné, invitation.</summary>
public static class UsageStatsService
{
    /// <summary>Objet de l'email d'invitation.</summary>
    public const string InviteSubject = "Essaie Plume : dicte tes emails et messages 3 fois plus vite";

    private const double WordsPerMinute = 40.0; // hypothèse de frappe

    /// <summary>Compte les mots (séparés par des blancs).</summary>
    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>Minutes gagnées estimées (frappe ~40 mots/min).</summary>
    public static double MinutesSaved(long wordCount) => wordCount / WordsPerMinute;

    /// <summary>Formate le temps gagné : « ~0 min », « ~10 min », « ~1 h », « ~1 h 01 », « ~4 h 10 ».</summary>
    public static string FormatTimeSaved(long wordCount)
    {
        var totalMin = (int)Math.Round(MinutesSaved(wordCount));
        if (totalMin < 60) return $"~{totalMin} min";
        int h = totalMin / 60, m = totalMin % 60;
        return m == 0 ? $"~{h} h" : $"~{h} h {m:D2}";
    }

    /// <summary>Corps de l'email d'invitation. <paramref name="attachmentIncluded"/> adapte le
    /// paragraphe d'installation : installateur joint à l'email (brouillon Outlook), ou à demander
    /// à l'expéditeur (repli mailto, qui ne sait pas joindre de fichier).</summary>
    public static string BuildInviteBody(bool attachmentIncluded)
    {
        var installation = attachmentIncluded
            ? "Pour l'essayer : lance le fichier PlumeSetup joint à cet email (2 minutes, pas besoin de droits administrateur)."
            : "Pour l'essayer : demande-moi le fichier d'installation PlumeSetup (2 minutes à installer, pas besoin de droits administrateur).";
        return
            "Salut,\n\n"
            + "Je te partage Plume, l'outil de dictée vocale que j'utilise tous les jours : "
            + "j'appuie sur un raccourci, je parle, et le texte — propre, ponctué, sans hésitations — "
            + "s'écrit tout seul là où se trouve mon curseur. Outlook, Teams, Word, navigateur : partout.\n\n"
            + "Ce que ça change au quotidien :\n"
            + "  - Un email dicté en 30 secondes au lieu de 3 minutes au clavier.\n"
            + "  - Le texte sort déjà nettoyé : ponctuation, majuscules, tics de langage supprimés.\n"
            + "  - Un clic sur la pastille reformule le texte sélectionné (corriger, ton pro, email prêt à envoyer…).\n"
            + "  - Un assistant vocal intégré répond à tes questions à la voix (Ctrl+Alt+A).\n"
            + "  - Léger et discret : il vit dans la barre des tâches et se fait oublier.\n\n"
            + installation + "\n"
            + "Au premier lancement, un guide t'accompagne en 3 étapes, dont la création d'une clé gratuite — tout est expliqué.\n\n"
            + "Astuce : si Windows affiche « Windows a protégé votre ordinateur », clique sur "
            + "« Informations complémentaires » puis « Exécuter quand même » — c'est notre outil interne, il n'est pas signé.\n\n"
            + "À bientôt !";
    }

    /// <summary>Construit l'URI mailto: d'invitation (objet + corps encodés). Repli sans pièce
    /// jointe : le protocole mailto ne sait pas joindre de fichier.</summary>
    public static string BuildInviteUri()
        => $"mailto:?subject={Uri.EscapeDataString(InviteSubject)}&body={Uri.EscapeDataString(BuildInviteBody(attachmentIncluded: false))}";
}
