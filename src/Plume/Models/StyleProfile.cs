namespace Plume.Models;

/// <summary>Mode d'un profil : Dictation = assistant vocal (dictée) ; Agent = « Commande écrite »
/// (reformule la sélection selon le prompt figé) ; Command = « Commande vocale » (applique à la sélection
/// une instruction dictée à la voix).</summary>
public enum ProfileMode { Dictation, Agent, Command }

/// <summary>Application à laquelle un profil est restreint. None = actif partout (défaut).
/// L'ordre fait foi : il correspond aux index du menu déroulant des réglages.</summary>
public enum TargetApp { None, Outlook, Teams, Browser }

/// <summary>Profil de style de nettoyage (nom + prompt éditable).</summary>
public class StyleProfile
{
    public string Id { get; set; } = "";       // "standard", ou guid pour les profils perso
    public string Name { get; set; } = "";      // libellé affiché, éditable
    public string Prompt { get; set; } = "";     // system prompt de nettoyage/reformulation
    public bool IsBuiltIn { get; set; }         // true = Standard (resettable, non supprimable)
    public HotkeyConfig? Hotkey { get; set; }   // null = aucun raccourci dédié à ce profil
    public ProfileMode? Mode { get; set; }      // null = non choisi (brouillon) ; Dictation = dictée ; Agent = reformuler
    public bool Enabled { get; set; } = true;  // false = profil désactivé (non proposé à l'activation)
    public double Temperature { get; set; } = 0.2; // créativité du nettoyage/reformulation (0 = fidèle, 1 = libre)
    public TargetApp TargetApp { get; set; } = TargetApp.None; // None = partout ; sinon le raccourci ne marche que si l'app ciblée est au premier plan
    // Déprécié : ancien 2e raccourci V4-A. Lu uniquement pour migrer vers Mode=Agent, puis remis à null.
    public HotkeyConfig? TransformHotkey { get; set; }
}
