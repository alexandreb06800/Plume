using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Plume.Models;

namespace Plume.Services;

/// <summary>Charge et sauvegarde la config dans %APPDATA%\Plume\config.json.</summary>
public class ConfigService
{
    private readonly string _dir;
    private readonly object _saveLock = new();
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public ConfigService() : this(DefaultDir()) { }

    /// <summary>Constructeur testable avec dossier explicite.</summary>
    public ConfigService(string dir) => _dir = dir;

    private static string DefaultDir() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Plume");

    public string ConfigPath => Path.Combine(_dir, "config.json");

    public AppConfig Load()
    {
        AppConfig cfg;
        bool? legacyCleanup = null;
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                cfg = JsonSerializer.Deserialize<AppConfig>(json, JsonOpts) ?? AppConfig.CreateDefault();
                legacyCleanup = ReadLegacyCleanupEnabled(json);
            }
            else
            {
                cfg = AppConfig.CreateDefault();
            }
        }
        catch
        {
            // Fichier corrompu ou illisible : on repart des défauts, jamais de crash.
            cfg = AppConfig.CreateDefault();
        }

        cfg.OpenRouterApiKey = Unprotect(cfg.OpenRouterApiKey);
        cfg.GroqApiKey = Unprotect(cfg.GroqApiKey);
        cfg.UpdateToken = Unprotect(cfg.UpdateToken);
        StyleService.Normalize(cfg, legacyCleanup);
        MigrateBadgePosition(cfg);
        return cfg;
    }

    /// <summary>V8-H1 : déchiffre une clé API stockée (préfixe <c>enc:</c>). Une valeur sans préfixe est
    /// une ancienne config en clair (migration transparente) et est retournée telle quelle. Un blob
    /// illisible (autre machine/profil Windows, ou corrompu) rend la clé considérée absente — jamais de crash.</summary>
    private static string? Unprotect(string? stored)
    {
        if (stored is null || !stored.StartsWith("enc:", StringComparison.Ordinal)) return stored;
        try
        {
            var bytes = Convert.FromBase64String(stored["enc:".Length..]);
            var clear = ProtectedData.Unprotect(bytes, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clear);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>V8-H1 : chiffre une clé API pour le stockage (portée utilisateur Windows, DPAPI).</summary>
    private static string? Protect(string? clear)
    {
        if (string.IsNullOrEmpty(clear)) return clear;
        var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(clear), null, DataProtectionScope.CurrentUser);
        return "enc:" + Convert.ToBase64String(bytes);
    }

    /// <summary>Migration V8-J : l'ancien champ unique <see cref="AppConfig.BadgePosition"/> devient une
    /// entrée de <see cref="AppConfig.BadgePositions"/> pour l'écran principal, puis est mis à null
    /// (ne sera plus jamais réécrit une fois migré).</summary>
    private static void MigrateBadgePosition(AppConfig cfg)
    {
        if (cfg.BadgePosition is null) return;
        var primaryId = Win32Screens.Primary()?.DeviceName;
        if (string.IsNullOrEmpty(primaryId)) return; // écran principal indéterminable : on retentera au prochain lancement
        if (!cfg.BadgePositions.ContainsKey(primaryId))
            cfg.BadgePositions[primaryId] = cfg.BadgePosition;
        cfg.BadgePosition = null;
    }

    /// <summary>Lit de façon tolérante l'ancien champ « cleanupEnabled » (V2.5) pour la migration des profils.</summary>
    private static bool? ReadLegacyCleanupEnabled(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("cleanupEnabled", out var v)
                && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False))
                return v.GetBoolean();
        }
        catch { /* ignore : pas de champ legacy */ }
        return null;
    }

    public void Save(AppConfig config)
    {
        lock (_saveLock)
        {
            Directory.CreateDirectory(_dir);
            var json = JsonSerializer.Serialize(config, JsonOpts);
            // V8-H1 : les clés API sont chiffrées sur disque ; l'objet en mémoire (config) n'est jamais
            // muté, les services qui le tiennent continuent de voir les clés en clair.
            var node = JsonNode.Parse(json)!.AsObject();
            node["openRouterApiKey"] = Protect(config.OpenRouterApiKey);
            node["groqApiKey"] = Protect(config.GroqApiKey);
            node["updateToken"] = Protect(config.UpdateToken);
            // Écriture atomique : jamais de config.json tronqué si le process est tué en pleine écriture
            // (Load repartirait silencieusement des défauts, clés API comprises).
            var tmp = ConfigPath + ".tmp";
            File.WriteAllText(tmp, node.ToJsonString(JsonOpts));
            File.Move(tmp, ConfigPath, overwrite: true);
        }
    }

    /// <summary>Clé API : config d'abord, sinon variable d'env OPENROUTER_API_KEY.</summary>
    public string? ResolveApiKey(AppConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.OpenRouterApiKey)) return config.OpenRouterApiKey;
        var env = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");
        return string.IsNullOrWhiteSpace(env) ? null : env;
    }

    /// <summary>Clé Groq (transcription rapide) : config d'abord, sinon variable d'env GROQ_API_KEY ; null si absente.</summary>
    public string? ResolveGroqKey(AppConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.GroqApiKey)) return config.GroqApiKey;
        var env = Environment.GetEnvironmentVariable("GROQ_API_KEY");
        return string.IsNullOrWhiteSpace(env) ? null : env;
    }

    /// <summary>Modèle de nettoyage garanti utilisable avec les clés disponibles. Sans clé OpenRouter,
    /// un modèle non préfixé « groq/ » (ex. le défaut openai/gpt-4o-mini) bloquerait la dictée dès
    /// MissingKeyMessage — cas typique : onboarding où seule la clé Groq du guide a été saisie.
    /// Renvoie alors le premier modèle Groq du catalogue ; sinon le modèle inchangé. Pur et testable
    /// (la présence des clés est résolue par l'appelant).</summary>
    public static string CleanupModelUtilisable(string cleanupModel, bool hasOpenRouterKey)
    {
        if (hasOpenRouterKey) return cleanupModel;
        if (cleanupModel.StartsWith(CleanupService.GroqPrefix, StringComparison.OrdinalIgnoreCase)) return cleanupModel;
        var groq = ModelCatalog.Cleanup.FirstOrDefault(
            m => m.Id.StartsWith(CleanupService.GroqPrefix, StringComparison.OrdinalIgnoreCase));
        return groq?.Id ?? cleanupModel;
    }

    /// <summary>Message d'erreur FR si une clé nécessaire au flux manque, sinon null. Règles de routage
    /// (statiques, voir TranscriptionService/CleanupService) : la STT passe par Groq si une clé Groq est
    /// présente, sinon par OpenRouter ; le nettoyage passe par Groq si le modèle est préfixé « groq/ »,
    /// sinon par OpenRouter. Une configuration 100 % Groq ne doit PAS exiger de clé OpenRouter.</summary>
    public string? MissingKeyMessage(AppConfig config, string cleanupModel)
    {
        bool hasOpenRouter = ResolveApiKey(config) is not null;
        bool hasGroq = ResolveGroqKey(config) is not null;
        bool cleanupOnGroq = cleanupModel.StartsWith(CleanupService.GroqPrefix, StringComparison.OrdinalIgnoreCase);

        if (!hasGroq && !hasOpenRouter)
            return "Renseigne une clé API (Groq ou OpenRouter) dans les paramètres.";
        if (cleanupOnGroq && !hasGroq)
            return "Le modèle choisi tourne sur Groq : renseigne ta clé Groq dans les paramètres.";
        if (!cleanupOnGroq && !hasOpenRouter)
            return "Le modèle choisi passe par OpenRouter : renseigne ta clé OpenRouter dans les paramètres.";
        return null;
    }

    /// <summary>Indication affichée dans les Réglages, sous le modèle de nettoyage, quand la clé de son
    /// fournisseur manque — pour la demander au moment du choix, pas à l'échec de la dictée suivante.
    /// Null si le modèle est utilisable tel quel. Même routage que <see cref="MissingKeyMessage"/>.</summary>
    public string? CleanupKeyHint(AppConfig config, string cleanupModel)
    {
        bool cleanupOnGroq = cleanupModel.StartsWith(CleanupService.GroqPrefix, StringComparison.OrdinalIgnoreCase);
        if (cleanupOnGroq)
            return ResolveGroqKey(config) is null ? "Ce modèle tourne sur Groq : renseigne ta clé Groq ci-dessus." : null;
        return ResolveApiKey(config) is null ? "Ce modèle passe par OpenRouter : renseigne ta clé OpenRouter ci-dessus." : null;
    }
}
