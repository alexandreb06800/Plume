namespace Plume.Models;

/// <summary>Configuration persistée de Plume.</summary>
public class AppConfig
{
    public string? OpenRouterApiKey { get; set; }
    /// <summary>Clé Groq optionnelle : si renseignée, la transcription passe par Groq en direct (bien plus rapide).</summary>
    public string? GroqApiKey { get; set; }
    public string SttModel { get; set; } = "openai/whisper-large-v3-turbo";
    public string CleanupModel { get; set; } = "openai/gpt-4o-mini";
    public HotkeyConfig Hotkey { get; set; } = new();
    public bool StartWithWindows { get; set; } = true;
    public string? InputDevice { get; set; }
    public string DictationMode { get; set; } = "toggle"; // "toggle" | "hold"
    public int MaxListeningSeconds { get; set; } = 60; // durée max d'une dictée avant coupure auto (valeurs proposées : 30/60/120/300)
    /// <summary>Assistant vocal (V4-C) activé : si vrai et AgentHotkey non nul, le raccourci dédié est enregistré.</summary>
    public bool AgentEnabled { get; set; } = true;
    /// <summary>Raccourci global de l'assistant vocal. null = aucun. Défaut Ctrl+Alt+A.</summary>
    public HotkeyConfig? AgentHotkey { get; set; } = new() { Modifiers = new() { "Ctrl", "Alt" }, Key = "A" };
    /// <summary>Modèle IA de l'assistant vocal ; null/vide = modèle de nettoyage global (CleanupModel).</summary>
    public string? AgentModel { get; set; }
    public List<string> Vocabulary { get; set; } = new();
    public List<ReplacementRule> Replacements { get; set; } = new();
    /// <summary>Active les commandes vocales de saut de ligne dictées (« à la ligne », « nouveau paragraphe »). V8-E, ON par défaut.</summary>
    public bool LineBreakCommandsEnabled { get; set; } = true;
    public List<StyleProfile> Profiles { get; set; } = new();       // legacy : désérialisé puis migré vers SelectionStyles par StyleService
    public string ActiveProfileId { get; set; } = "";               // legacy : conservé pour la désérialisation des anciennes configs, ignoré après migration
    /// <summary>Prompt de nettoyage de la dictée (cœur de Plume). Renseigné par StyleService.Normalize.</summary>
    public string DictationPrompt { get; set; } = "";
    /// <summary>Créativité du nettoyage de la dictée (0 = fidèle, 1 = libre).</summary>
    public double DictationTemperature { get; set; } = 0.2;
    /// <summary>Styles du menu de sélection (remplace l'ancien modèle « profils »).</summary>
    public List<SelectionStyle> SelectionStyles { get; set; } = new();
    /// <summary>Afficher la pastille flottante Plume (créée en différé après le boot).</summary>
    public bool ShowBadge { get; set; } = true;
    /// <summary>Vrai (historique) = une pastille par écran connecté ; faux = une seule pastille, sur l'écran principal.</summary>
    public bool BadgeOnAllScreens { get; set; } = true;
    /// <summary>Active les tops sonores discrets début/fin d'écoute (V8-K). OFF par défaut.</summary>
    public bool TickSoundsEnabled { get; set; } = false;
    /// <summary>Id du son joué quand le micro devient prêt (catalogue <see cref="Services.SoundCatalog"/>).</summary>
    public string StartTickSound { get; set; } = "discret";
    /// <summary>Id du son joué à l'insertion réussie d'une dictée classique.</summary>
    public string EndTickSound { get; set; } = "discret";
    /// <summary>Legacy (V8-J) : ancienne position unique de la pastille. Conservé pour la désérialisation ;
    /// migré vers <see cref="BadgePositions"/> et mis à null au chargement (voir ConfigService.Load).</summary>
    public BadgePosition? BadgePosition { get; set; }
    /// <summary>Positions mémorisées des pastilles, une par écran (clé = identifiant d'écran, ex. « \\.\DISPLAY1 »). V8-J.</summary>
    public Dictionary<string, BadgePosition> BadgePositions { get; set; } = new();
    /// <summary>Vrai une fois les styles intégrés injectés : évite de les réinjecter (donc de pouvoir les supprimer).</summary>
    public bool BuiltInStylesSeeded { get; set; }
    public string ThemeMode { get; set; } = "auto";   // "auto" (suit Windows) | "light" | "dark"
    public UsageStats Stats { get; set; } = new();
    /// <summary>Journal de diagnostic opt-in (V8-H2), OFF par défaut. Jamais le texte dicté ni les clés.</summary>
    public bool DiagnosticsLogEnabled { get; set; } = false;

    public static AppConfig CreateDefault() => new();
}

/// <summary>Règle de remplacement exacte : « from » (raccourci dicté) → « to » (texte inséré).</summary>
public class ReplacementRule
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";

    public override string ToString() => $"{From} → {(To ?? "").Replace("\r\n", "\n").Replace("\n", " ↵ ")}";
}

/// <summary>Combinaison de raccourci : modificateurs + touche.</summary>
public class HotkeyConfig
{
    public List<string> Modifiers { get; set; } = new() { "Alt" };
    public string Key { get; set; } = "Space";
}

/// <summary>Compteurs d'usage cumulés (statistiques).</summary>
public class UsageStats
{
    public long DictationCount { get; set; } // nombre de dictées insérées
    public long WordCount { get; set; }       // total de mots dictés
}
