using System.IO;
using System.Linq;
using Plume.Models;
using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class ConfigServiceTests
{
    private static ConfigService NewServiceInTemp(out string dir)
    {
        dir = Path.Combine(Path.GetTempPath(), "PlumeTest_" + Guid.NewGuid().ToString("N"));
        return new ConfigService(dir);
    }

    [Fact]
    public void Load_SansFichier_RetourneDefauts_V6()
    {
        var svc = NewServiceInTemp(out _);
        var cfg = svc.Load();

        Assert.Equal("openai/whisper-large-v3-turbo", cfg.SttModel);
        Assert.Contains("Alt", cfg.Hotkey.Modifiers);
        Assert.Equal("Space", cfg.Hotkey.Key);
        Assert.True(cfg.StartWithWindows);
        Assert.Equal(StyleCatalog.DictationPrompt, cfg.DictationPrompt);
        Assert.Equal(6, cfg.SelectionStyles.Count);         // 6 intégrés seedés
        Assert.True(cfg.ShowBadge);
        Assert.True(cfg.LineBreakCommandsEnabled);          // V8-E : activé par défaut
    }

    [Fact]
    public void Save_PuisLoad_PreserveStylesEtDictee()
    {
        var svc = NewServiceInTemp(out _);
        var cfg = AppConfig.CreateDefault();
        StyleService.Normalize(cfg, null);
        cfg.DictationPrompt = "MON PROMPT";
        cfg.DictationTemperature = 0.5;
        cfg.ShowBadge = false;
        cfg.BadgePosition = new BadgePosition { X = 100, Y = 200 };
        cfg.SelectionStyles.Add(new SelectionStyle { Id = "perso", Name = "Perso", Prompt = "p", WithVoice = true, IsFavorite = true });

        svc.Save(cfg);
        var reloaded = svc.Load();

        Assert.Equal("MON PROMPT", reloaded.DictationPrompt);
        Assert.Equal(0.5, reloaded.DictationTemperature);
        Assert.False(reloaded.ShowBadge);
        // V8-J : l'ancien champ unique est migré vers BadgePositions (par écran) et mis à null au chargement.
        Assert.Null(reloaded.BadgePosition);
        Assert.Equal(100, reloaded.BadgePositions.Values.Single().X);
        Assert.Equal(200, reloaded.BadgePositions.Values.Single().Y);
        var perso = reloaded.SelectionStyles.Single(s => s.Id == "perso");
        Assert.True(perso.WithVoice);
        Assert.True(perso.IsFavorite);
    }

    [Fact]
    public void Load_AncienneConfigProfils_MigreVersStyles()
    {
        var svc = NewServiceInTemp(out var dir);
        Directory.CreateDirectory(dir);
        // Ancienne config V5 : un profil standard (dictée) + un profil Agent (reformulation).
        File.WriteAllText(Path.Combine(dir, "config.json"), """
        {
          "hotkey": { "modifiers": ["Ctrl"], "key": "Space" },
          "profiles": [
            { "id": "standard", "name": "Standard", "prompt": "PROMPT DICTEE", "mode": "Dictation", "isBuiltIn": true,
              "hotkey": { "modifiers": ["Ctrl"], "key": "Space" } },
            { "id": "email", "name": "Email pro", "prompt": "PROMPT EMAIL", "mode": "Dictation", "enabled": true }
          ]
        }
        """);

        var cfg = svc.Load();

        Assert.Equal("PROMPT DICTEE", cfg.DictationPrompt);         // "standard" → dictée globale
        Assert.DoesNotContain(cfg.SelectionStyles, s => s.Id == "standard");
        var email = cfg.SelectionStyles.Single(s => s.Id == "email");
        Assert.False(email.WithVoice);                             // profil Dictation perso → style ÉCRIT (spec §Migration)
        Assert.Equal("PROMPT EMAIL", email.Prompt);               // prompt conservé
        Assert.Equal(6, cfg.SelectionStyles.Count(s => s.IsBuiltIn)); // intégrés injectés
    }

    [Fact]
    public void Load_FichierCorrompu_RetourneDefautsSansCrash()
    {
        var svc = NewServiceInTemp(out var dir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "config.json"), "{ ceci n'est pas du json");
        var cfg = svc.Load();
        Assert.Equal("openai/whisper-large-v3-turbo", cfg.SttModel);
        Assert.Equal(6, cfg.SelectionStyles.Count);
    }

    [Fact]
    public void Load_AncienBadgePositionUnique_MigreVersBadgePositions()
    {
        var svc = NewServiceInTemp(out var dir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "config.json"), """
        {
          "badgePosition": { "x": 42, "y": 84 }
        }
        """);

        var cfg = svc.Load();

        Assert.Null(cfg.BadgePosition);
        Assert.Single(cfg.BadgePositions);
        var pos = cfg.BadgePositions.Values.Single();
        Assert.Equal(42, pos.X);
        Assert.Equal(84, pos.Y);
    }

    [Fact]
    public void Save_PuisLoad_PreserveBadgePositionsParEcran()
    {
        var svc = NewServiceInTemp(out _);
        var cfg = AppConfig.CreateDefault();
        cfg.BadgePositions[@"\\.\DISPLAY1"] = new BadgePosition { X = 10, Y = 20 };
        cfg.BadgePositions[@"\\.\DISPLAY2"] = new BadgePosition { X = 30, Y = 40 };

        svc.Save(cfg);
        var reloaded = svc.Load();

        Assert.Equal(2, reloaded.BadgePositions.Count);
        Assert.Equal(10, reloaded.BadgePositions[@"\\.\DISPLAY1"].X);
        Assert.Equal(40, reloaded.BadgePositions[@"\\.\DISPLAY2"].Y);
    }

    [Fact]
    public void Load_SansFichier_BadgePositionsVideEtLegacyNull()
    {
        var svc = NewServiceInTemp(out _);
        var cfg = svc.Load();
        Assert.Empty(cfg.BadgePositions);
        Assert.Null(cfg.BadgePosition);
    }

    [Fact]
    public void ResolveApiKey_PrefereConfig_PuisEnv()
    {
        var svc = NewServiceInTemp(out _);
        var cfg = AppConfig.CreateDefault();
        cfg.OpenRouterApiKey = "depuis-config";
        Assert.Equal("depuis-config", svc.ResolveApiKey(cfg));
        cfg.OpenRouterApiKey = null;
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", "depuis-env");
        Assert.Equal("depuis-env", svc.ResolveApiKey(cfg));
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", null);
    }

    [Fact]
    public void Save_PuisLoad_PreserveLesReglagesAgent()
    {
        var svc = NewServiceInTemp(out _);
        var cfg = AppConfig.CreateDefault();
        cfg.AgentEnabled = false;
        cfg.AgentHotkey = new HotkeyConfig { Modifiers = new() { "Ctrl", "Shift" }, Key = "J" };
        svc.Save(cfg);
        var loaded = svc.Load();
        Assert.False(loaded.AgentEnabled);
        Assert.Equal("J", loaded.AgentHotkey!.Key);
    }

    [Fact]
    public void Save_PuisLoad_PreserveLesChampsV2()
    {
        var svc = NewServiceInTemp(out _);
        var cfg = AppConfig.CreateDefault();
        cfg.DictationMode = "hold";
        cfg.Vocabulary = new() { "MotMetier", "SFTP" };
        cfg.Replacements = new() { new ReplacementRule { From = "emevem", To = "exemple.fr" } };
        svc.Save(cfg);
        var reloaded = svc.Load();
        Assert.Equal("hold", reloaded.DictationMode);
        Assert.Equal(new() { "MotMetier", "SFTP" }, reloaded.Vocabulary);
        Assert.Equal("emevem", reloaded.Replacements[0].From);
    }

    [Fact]
    public void Save_PuisLoad_PreserveLineBreakCommandsEnabled()
    {
        var svc = NewServiceInTemp(out _);
        var cfg = AppConfig.CreateDefault();
        cfg.LineBreakCommandsEnabled = false;
        svc.Save(cfg);
        var reloaded = svc.Load();
        Assert.False(reloaded.LineBreakCommandsEnabled);
    }

    [Fact]
    public void Save_PuisLoad_PreserveLesStats()
    {
        var svc = NewServiceInTemp(out _);
        var cfg = AppConfig.CreateDefault();
        cfg.Stats.DictationCount = 12;
        cfg.Stats.WordCount = 3400;
        svc.Save(cfg);
        var reloaded = svc.Load();
        Assert.Equal(12, reloaded.Stats.DictationCount);
        Assert.Equal(3400, reloaded.Stats.WordCount);
    }

    [Fact]
    public void Save_Concurrent_NeCorrompPasLeFichier()
    {
        var svc = NewServiceInTemp(out _);
        var cfg = AppConfig.CreateDefault();
        System.Threading.Tasks.Parallel.For(0, 50, _ => svc.Save(cfg));
        var reloaded = svc.Load();
        Assert.Equal("openai/whisper-large-v3-turbo", reloaded.SttModel);
    }

    // --- V8-H : chiffrement DPAPI des clés API au repos ---

    [Fact]
    public void Save_ChiffreLesClesApiSurDisque()
    {
        var svc = NewServiceInTemp(out var dir);
        var cfg = AppConfig.CreateDefault();
        cfg.OpenRouterApiKey = "sk-or-secret";
        cfg.GroqApiKey = "gsk-secret";

        svc.Save(cfg);
        var rawJson = File.ReadAllText(Path.Combine(dir, "config.json"));

        Assert.DoesNotContain("sk-or-secret", rawJson);
        Assert.DoesNotContain("gsk-secret", rawJson);
        Assert.Contains("enc:", rawJson);
        // La clé passée à Save() n'est jamais mutée : les services en mémoire gardent le clair.
        Assert.Equal("sk-or-secret", cfg.OpenRouterApiKey);
        Assert.Equal("gsk-secret", cfg.GroqApiKey);
    }

    [Fact]
    public void Save_PuisLoad_DechiffreLesClesApi()
    {
        var svc = NewServiceInTemp(out _);
        var cfg = AppConfig.CreateDefault();
        cfg.OpenRouterApiKey = "sk-or-secret";
        cfg.GroqApiKey = "gsk-secret";

        svc.Save(cfg);
        var reloaded = svc.Load();

        Assert.Equal("sk-or-secret", reloaded.OpenRouterApiKey);
        Assert.Equal("gsk-secret", reloaded.GroqApiKey);
    }

    [Fact]
    public void Load_CleEnClairAncienneConfig_UtiliseeTelleQuelleEtMigreeAuProchainSave()
    {
        var svc = NewServiceInTemp(out var dir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "config.json"), """
        {
          "openRouterApiKey": "en-clair-existant"
        }
        """);

        var cfg = svc.Load();
        Assert.Equal("en-clair-existant", cfg.OpenRouterApiKey);

        svc.Save(cfg);
        var rawJson = File.ReadAllText(Path.Combine(dir, "config.json"));
        Assert.DoesNotContain("en-clair-existant", rawJson);
        Assert.Contains("enc:", rawJson);
        Assert.Equal("en-clair-existant", svc.Load().OpenRouterApiKey); // re-déchiffre correctement ensuite
    }

    [Fact]
    public void Load_BlobChiffreCorrompu_CleConsidereeAbsenteSansCrash()
    {
        var svc = NewServiceInTemp(out var dir);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "config.json"), """
        {
          "groqApiKey": "enc:ceci-n-est-pas-un-blob-chiffre-valide"
        }
        """);

        var cfg = svc.Load();
        Assert.Null(cfg.GroqApiKey);
    }

    [Fact]
    public void Save_EcritureAtomique_PasDeResiduTmpEtRelisible()
    {
        var dir = Path.Combine(Path.GetTempPath(), "PlumeTests_" + Guid.NewGuid().ToString("N"));
        try
        {
            var svc = new ConfigService(dir);
            var cfg = AppConfig.CreateDefault();
            cfg.Vocabulary.Add("ACME");

            svc.Save(cfg);
            svc.Save(cfg); // 2e écriture : le Move doit bien écraser le fichier existant

            Assert.False(File.Exists(svc.ConfigPath + ".tmp")); // pas de fichier temporaire résiduel
            var relu = svc.Load();
            Assert.Contains("ACME", relu.Vocabulary);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    // --- Task 7 : n'exiger que les clés réellement nécessaires selon le flux ---

    [Fact]
    public void MissingKeyMessage_ToutGroq_AucuneCleOpenRouterRequise()
    {
        var (svc, cfg) = ConfigSansEnv();
        cfg.GroqApiKey = "gsk-test";
        Assert.Null(svc.MissingKeyMessage(cfg, "groq/llama-3.3-70b-versatile"));
    }

    [Fact]
    public void MissingKeyMessage_AucuneCle_MessageGenerique()
    {
        var (svc, cfg) = ConfigSansEnv();
        var msg = svc.MissingKeyMessage(cfg, "openai/gpt-4o-mini");
        Assert.NotNull(msg);
        Assert.Contains("clé API", msg);
    }

    [Fact]
    public void MissingKeyMessage_ModeleOpenRouterSansCleOpenRouter_MessageOpenRouter()
    {
        var (svc, cfg) = ConfigSansEnv();
        cfg.GroqApiKey = "gsk-test";
        var msg = svc.MissingKeyMessage(cfg, "openai/gpt-4o-mini");
        Assert.NotNull(msg);
        Assert.Contains("OpenRouter", msg);
    }

    [Fact]
    public void MissingKeyMessage_ModeleGroqSansCleGroq_MessageGroq()
    {
        var (svc, cfg) = ConfigSansEnv();
        cfg.OpenRouterApiKey = "sk-or-test";
        var msg = svc.MissingKeyMessage(cfg, "groq/llama-3.3-70b-versatile");
        Assert.NotNull(msg);
        Assert.Contains("Groq", msg);
    }

    [Fact]
    public void CleanupKeyHint_ModeleOpenRouterSansCle_DemandeLaCleOpenRouter()
    {
        var (svc, cfg) = ConfigSansEnv();
        cfg.GroqApiKey = "gsk-test";
        Assert.Contains("OpenRouter", svc.CleanupKeyHint(cfg, "openai/gpt-4o-mini"));
        cfg.OpenRouterApiKey = "sk-or-test";
        Assert.Null(svc.CleanupKeyHint(cfg, "openai/gpt-4o-mini"));
    }

    [Fact]
    public void CleanupKeyHint_ModeleGroq_NeDemandeQueLaCleGroq()
    {
        var (svc, cfg) = ConfigSansEnv();
        Assert.Contains("Groq", svc.CleanupKeyHint(cfg, "groq/qwen/qwen3.8-27b"));
        cfg.GroqApiKey = "gsk-test";
        Assert.Null(svc.CleanupKeyHint(cfg, "groq/qwen/qwen3.8-27b")); // aucune clé OpenRouter requise
    }

    /// <summary>Neutralise les variables d'env (une clé présente sur la machine de dev fausserait les cas « absent »).</summary>
    private static (ConfigService svc, AppConfig cfg) ConfigSansEnv()
    {
        Environment.SetEnvironmentVariable("OPENROUTER_API_KEY", null);
        Environment.SetEnvironmentVariable("GROQ_API_KEY", null);
        var dir = Path.Combine(Path.GetTempPath(), "PlumeTests_" + Guid.NewGuid().ToString("N"));
        return (new ConfigService(dir), AppConfig.CreateDefault());
    }

    // --- CleanupModelUtilisable (correctif audit 2026-07-16 : onboarding 100 % Groq) ---

    [Fact]
    public void CleanupModelUtilisable_SansOpenRouter_BasculeSurUnModeleGroq()
    {
        var result = ConfigService.CleanupModelUtilisable("openai/gpt-4o-mini", hasOpenRouterKey: false);
        Assert.StartsWith("groq/", result);
    }

    [Fact]
    public void CleanupModelUtilisable_AvecOpenRouter_NeChangeRien()
    {
        Assert.Equal("openai/gpt-4o-mini",
            ConfigService.CleanupModelUtilisable("openai/gpt-4o-mini", hasOpenRouterKey: true));
    }

    [Fact]
    public void CleanupModelUtilisable_DejaSurGroq_NeChangeRien()
    {
        Assert.Equal("groq/openai/gpt-oss-120b",
            ConfigService.CleanupModelUtilisable("groq/openai/gpt-oss-120b", hasOpenRouterKey: false));
    }

    [Fact]
    public void CleanupModelUtilisable_SansOpenRouter_BasculeSurGptOssGroq()
    {
        // Prise en main « 100 % Groq » : défaut sur un modèle « Production » chez Groq (décision du 2026-10-16).
        Assert.Equal("groq/openai/gpt-oss-120b",
            ConfigService.CleanupModelUtilisable("openai/gpt-4o-mini", hasOpenRouterKey: false));
    }

    [Fact]
    public void Load_ModelesRetires_RemplacesPartout()
    {
        var svc = NewServiceInTemp(out _);
        var cfg = AppConfig.CreateDefault();
        cfg.CleanupModel = "groq/llama-3.3-70b-versatile";
        cfg.AgentModel = "groq/llama-3.1-8b-instant";
        cfg.BuiltInStylesSeeded = true;
        cfg.SelectionStyles = new()
        {
            new SelectionStyle { Id = "perso-1", Name = "Perso 1", Prompt = "p", Model = "google/gemini-2.0-flash-001" },
            new SelectionStyle { Id = "perso-2", Name = "Perso 2", Prompt = "p", Model = "mon/modele-perso" },
        };
        svc.Save(cfg);

        var lu = svc.Load();

        Assert.Equal("groq/openai/gpt-oss-120b", lu.CleanupModel);
        Assert.Equal("groq/openai/gpt-oss-120b", lu.AgentModel);
        Assert.Equal("openai/gpt-4o-mini", lu.SelectionStyles.Single(s => s.Id == "perso-1").Model);
        Assert.Equal("mon/modele-perso", lu.SelectionStyles.Single(s => s.Id == "perso-2").Model);
    }
}
