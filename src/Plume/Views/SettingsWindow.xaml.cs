using System.Collections.Generic;
using System.Linq;
using System.Windows;
using Plume.Models;
using Plume.Services;

namespace Plume.Views;

/// <summary>Fenêtre des Réglages : chrome commun (barre latérale, pied « Enregistré automatiquement »,
/// agrégation de la config) et délégation à un onglet par <see cref="Views.SettingsPanels"/> UserControl
/// (General/Dictionnaire/Style/APropos). La logique propre à chaque onglet vit dans le fichier partiel
/// correspondant : SettingsWindow.General.cs, .Dictionnaire.cs, .Styles.cs, .APropos.cs — qui référencent
/// les éléments nommés du panneau via le champ généré (ex. <c>PanelGeneral.SttCombo</c>, modificateur par
/// défaut <c>internal</c>, même assembly).</summary>
public partial class SettingsWindow : Window
{
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    private readonly Action<AppConfig> _onSaved;
    private readonly Func<string?> _getLastText;

    private bool _loaded; // vrai une fois l'UI initialisée : empêche les sauvegardes pendant le chargement
    private Dictionary<string, DayUsage>? _days; // lu paresseusement à l'ouverture (jamais au boot)

    public SettingsWindow(ConfigService configService, AppConfig config, Action<AppConfig> onSaved,
                          Func<string?> getLastText)
    {
        InitializeComponent();
        AppLogo.Apply(this); // icône de fenêtre (barre des tâches / titre)

        SourceInitialized += (_, _) =>
        {
            var theme = ((App)Application.Current).Theme;
            Services.DarkTitleBar.Apply(this, theme?.CurrentDark ?? false);
            if (theme is not null)
            {
                Action<bool> onChanged = dark => Dispatcher.Invoke(() => Services.DarkTitleBar.Apply(this, dark));
                theme.Changed += onChanged;
                Closed += (_, _) => theme.Changed -= onChanged;
            }
        };

        _configService = configService;
        _config = config;
        _onSaved = onSaved;
        _getLastText = getLastText;

        InitGeneral();
        InitDictionnaire();
        InitStyles();
        InitAbout();

        // Navigation sidebar → bascule des panneaux.
        NavList.SelectionChanged += (_, _) =>
        {
            PanelGeneral.Visibility = NavList.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
            PanelDico.Visibility    = NavList.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
            PanelStyle.Visibility   = NavList.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
            PanelAbout.Visibility   = NavList.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
        };

        // Statistiques (pied de sidebar : temps gagné + détail, selon la période choisie).
        RefreshSideStats();
        PeriodWeek.Checked += (_, _) => RefreshSideStats();
        PeriodMonth.Checked += (_, _) => RefreshSideStats();
        PeriodTotal.Checked += (_, _) => RefreshSideStats();
        InviteButton.Click += (_, _) => OpenInvite();

        _loaded = true;
    }

    /// <summary>Écrit l'état courant de l'UI dans `_config` (sans effet de bord ni fermeture).
    /// N'intègre PAS un brouillon non validé dans les profils.</summary>
    private void BuildConfigFromUi()
    {
        // En édition avec une saisie → nouvelle clé ; sinon on conserve la clé existante.
        if (PanelGeneral.ApiKeyEditPanel.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(PanelGeneral.ApiKeyBox.Password))
            _config.OpenRouterApiKey = PanelGeneral.ApiKeyBox.Password;
        if (PanelGeneral.GroqKeyEditPanel.Visibility == Visibility.Visible && !string.IsNullOrWhiteSpace(PanelGeneral.GroqKeyBox.Password))
            _config.GroqApiKey = PanelGeneral.GroqKeyBox.Password;
        _config.SttModel = ResolveComboModel(PanelGeneral.SttCombo, ModelCatalog.Stt, _config.SttModel);
        _config.CleanupModel = ResolveComboModel(PanelGeneral.CleanupCombo, ModelCatalog.Cleanup, _config.CleanupModel);
        _config.StartWithWindows = PanelGeneral.StartupBox.IsChecked == true;
        _config.DictationMode = PanelGeneral.ModeBox.SelectedIndex == 1 ? "hold" : "toggle";
        _config.MaxListeningSeconds = IndexToMaxSeconds(PanelGeneral.MaxDurationBox.SelectedIndex);
        _config.AgentEnabled = PanelGeneral.AgentEnabledBox.IsChecked == true;
        _config.AgentModel = ModelComboId(PanelGeneral.AgentModelBox.SelectedIndex); // modèle de l'assistant vocal (null = global)
        // _config.AgentHotkey est déjà posé par la capture / Effacer.
        _config.ThemeMode = PanelGeneral.ThemeBox.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "auto" };
        _config.InputDevice = PanelGeneral.MicBox.SelectedIndex <= 0 ? null : PanelGeneral.MicBox.SelectedItem as string;
        _config.Vocabulary = PanelDico.Vocabulary.ToList();
        _config.Replacements = PanelDico.GetReplacementRules();
        _config.LineBreakCommandsEnabled = PanelDico.LineBreakCommandsEnabled;

        _config.DictationPrompt = PanelGeneral.DictationPromptBox.Text;
        _config.DictationTemperature = PanelGeneral.DictationTempSlider.Value;
        _config.ShowBadge = PanelGeneral.ShowBadgeBox.IsChecked == true;
        _config.BadgeOnAllScreens = PanelGeneral.BadgeScreensBox.SelectedIndex != 1;
        _config.TickSoundsEnabled = PanelGeneral.TickSoundsBox.IsChecked == true;
        _config.StartTickSound = SoundComboId(PanelGeneral.StartTickBox.SelectedIndex);
        _config.EndTickSound = SoundComboId(PanelGeneral.EndTickBox.SelectedIndex);
        _config.DiagnosticsLogEnabled = PanelGeneral.DiagLogBox.IsChecked == true;
        // Les styles vivent dans _styles ; ne pas y reporter le brouillon non enregistré.
        _config.SelectionStyles = _styles
            .Where(r => _draftStyle is null || r.Style.Id != _draftStyle.Id)
            .Select(r => r.Style).ToList();
    }

    /// <summary>Sauvegarde immédiate : état UI → config → disque → notification. Ne ferme jamais.</summary>
    private void Persist()
    {
        if (!_loaded) return; // ignore les événements déclenchés pendant l'initialisation
        BuildConfigFromUi();
        try { StartupService.SetEnabled(_config.StartWithWindows, Environment.ProcessPath ?? ""); }
        catch { /* registre indisponible : on ne bloque jamais */ }
        _configService.Save(_config);
        _onSaved(_config);
    }

    /// <summary>Rafraîchit le pied de sidebar (temps gagné + détail) selon la période choisie (Sem/Mois/Tot).
    /// « Tot » utilise les cumuls historiques (`_config.Stats`, instantané) ; Sem/Mois lisent le fichier
    /// d'usage journalier (chargé une seule fois par ouverture des Réglages).</summary>
    private void RefreshSideStats()
    {
        _days ??= DailyUsageService.Load();
        long dict, words;
        if (PeriodTotal.IsChecked == true)
        {
            dict = _config.Stats.DictationCount;   // cumuls historiques : instantané, zéro migration
            words = _config.Stats.WordCount;
        }
        else
        {
            var from = PeriodWeek.IsChecked == true ? DateTime.Today.AddDays(-6) : DateTime.Today.AddDays(-29);
            (dict, words) = DailyUsageService.Sum(_days, from, DateTime.Today);
        }
        SideStatTime.Text = UsageStatsService.FormatTimeSaved(words);
        SideStatDetail.Text = $"{dict:N0} dictée{(dict > 1 ? "s" : "")} · {words:N0} mot{(words > 1 ? "s" : "")}";
    }

    /// <summary>Invitation d'un collègue : brouillon Outlook avec l'installateur en pièce jointe
    /// quand c'est possible (Outlook présent + PlumeSetup-*.exe à côté de l'app, déposé là par
    /// l'installateur lui-même) ; sinon repli sur le client mail par défaut via mailto (sans pièce
    /// jointe — le protocole ne le permet pas). Jamais de crash, jamais d'envoi automatique.</summary>
    private void OpenInvite()
    {
        var setup = FindSetupNextToApp();
        if (setup is not null && TryOutlookInvite(setup)) return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(UsageStatsService.BuildInviteUri()) { UseShellExecute = true });
        }
        catch { /* pas de client mail configuré : on n'interrompt rien */ }
    }

    /// <summary>Installateur le plus récent posé à côté de l'exe (null si absent — ex. lancement dév).</summary>
    private static string? FindSetupNextToApp()
    {
        try
        {
            return System.IO.Directory.EnumerateFiles(AppContext.BaseDirectory, "PlumeSetup-*.exe")
                .OrderByDescending(System.IO.File.GetLastWriteTimeUtc)
                .FirstOrDefault();
        }
        catch { return null; }
    }

    /// <summary>Compose un brouillon Outlook (COM en liaison tardive : aucune dépendance) avec la
    /// pièce jointe, et l'AFFICHE seulement — l'utilisateur choisit le destinataire et envoie.</summary>
    private static bool TryOutlookInvite(string setupPath)
    {
        try
        {
            var outlookType = Type.GetTypeFromProgID("Outlook.Application");
            if (outlookType is null) return false; // Outlook absent du poste
            dynamic outlook = Activator.CreateInstance(outlookType)!;
            dynamic mail = outlook.CreateItem(0); // 0 = olMailItem
            mail.Subject = UsageStatsService.InviteSubject;
            mail.Body = UsageStatsService.BuildInviteBody(attachmentIncluded: true);
            mail.Attachments.Add(setupPath);
            mail.Display(false); // fenêtre de composition non modale
            return true;
        }
        catch { return false; } // Outlook indisponible/récalcitrant : le repli mailto prend la main
    }
}
