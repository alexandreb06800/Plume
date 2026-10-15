using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Plume.Models;
using Plume.Services;

namespace Plume.Views;

/// <summary>Logique de l'onglet « Général » : clés API, modèles, raccourcis (dictée + assistant vocal),
/// sons, diagnostic, apparence &amp; système. Référence les éléments du panneau via <c>PanelGeneral.X</c>.</summary>
public partial class SettingsWindow
{
    private bool _capturingDictation; // capture du raccourci de dictée
    private bool _capturingAgent;
    private System.Windows.Threading.DispatcherTimer? _copyTimer;

    private void InitGeneral()
    {
        // Clé API : état « configurée » si une clé existe, sinon état « édition ».
        bool aUneCle = !string.IsNullOrWhiteSpace(_config.OpenRouterApiKey);
        SetApiKeyEditing(!aUneCle);
        PanelGeneral.ApiKeyEditButton.Click += (_, _) => { PanelGeneral.ApiKeyBox.Clear(); SetApiKeyEditing(true); };
        PanelGeneral.ApiKeyCancelButton.Click += (_, _) => { PanelGeneral.ApiKeyBox.Clear(); SetApiKeyEditing(false); };

        // Clé Groq (transcription rapide) : même logique, optionnelle.
        bool aUneCleGroq = !string.IsNullOrWhiteSpace(_config.GroqApiKey);
        SetGroqKeyEditing(!aUneCleGroq);
        PanelGeneral.GroqKeyEditButton.Click += (_, _) => { PanelGeneral.GroqKeyBox.Clear(); SetGroqKeyEditing(true); };
        PanelGeneral.GroqKeyCancelButton.Click += (_, _) => { PanelGeneral.GroqKeyBox.Clear(); SetGroqKeyEditing(false); };

        PanelGeneral.SttCombo.ItemsSource = ModelCatalog.Stt;
        PanelGeneral.CleanupCombo.ItemsSource = ModelCatalog.Cleanup;
        SetComboModel(PanelGeneral.SttCombo, _config.SttModel, ModelCatalog.Stt);
        SetComboModel(PanelGeneral.CleanupCombo, _config.CleanupModel, ModelCatalog.Cleanup);

        PanelGeneral.StartupBox.IsChecked = StartupService.IsEnabled();
        PanelGeneral.ModeBox.SelectedIndex = _config.DictationMode == "hold" ? 1 : 0;
        PanelGeneral.MaxDurationBox.SelectedIndex = MaxSecondsToIndex(_config.MaxListeningSeconds);
        PanelGeneral.ThemeBox.SelectedIndex = _config.ThemeMode switch { "light" => 1, "dark" => 2, _ => 0 };

        PanelGeneral.AgentEnabledBox.IsChecked = _config.AgentEnabled;
        RefreshAgentHotkeyButton();
        PopulateModelCombo(PanelGeneral.AgentModelBox);
        PanelGeneral.AgentModelBox.SelectedIndex = ModelComboIndex(_config.AgentModel);

        // Micro : « par défaut » + périphériques détectés.
        PanelGeneral.MicBox.Items.Add("Micro par défaut Windows");
        foreach (var d in AudioCaptureService.ListInputDevices()) PanelGeneral.MicBox.Items.Add(d);
        PanelGeneral.MicBox.SelectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(_config.InputDevice))
        {
            var idx = PanelGeneral.MicBox.Items.IndexOf(_config.InputDevice);
            if (idx >= 0) PanelGeneral.MicBox.SelectedIndex = idx;
        }

        // Général — Dictée.
        PanelGeneral.DictationPromptBox.Text = _config.DictationPrompt;
        PanelGeneral.DictationTempSlider.Value = _config.DictationTemperature;
        PanelGeneral.DictationTempValue.Text = _config.DictationTemperature.ToString("0.0");
        RefreshDictationHotkeyButton();
        PanelGeneral.DictationHotkeyButton.Click += (_, _) => StartDictationCapture();
        PanelGeneral.DictationResetButton.Click += (_, _) => { PanelGeneral.DictationPromptBox.Text = StyleCatalog.DictationPrompt; Persist(); };
        PanelGeneral.DictationPromptBox.LostFocus += (_, _) => Persist();
        PanelGeneral.DictationTempSlider.ValueChanged += (_, _) => { PanelGeneral.DictationTempValue.Text = PanelGeneral.DictationTempSlider.Value.ToString("0.0"); };
        PanelGeneral.DictationTempSlider.PreviewMouseUp += (_, _) => Persist();
        PanelGeneral.ShowBadgeBox.IsChecked = _config.ShowBadge;
        PanelGeneral.ShowBadgeBox.Checked += (_, _) => Persist();
        PanelGeneral.ShowBadgeBox.Unchecked += (_, _) => Persist();
        PanelGeneral.BadgeScreensBox.SelectedIndex = _config.BadgeOnAllScreens ? 0 : 1;
        PanelGeneral.BadgeScreensBox.SelectionChanged += (_, _) => Persist();

        // Général — Sons (V8-K).
        PanelGeneral.TickSoundsBox.IsChecked = _config.TickSoundsEnabled;
        PopulateSoundCombo(PanelGeneral.StartTickBox);
        PopulateSoundCombo(PanelGeneral.EndTickBox);
        PanelGeneral.StartTickBox.SelectedIndex = SoundComboIndex(_config.StartTickSound);
        PanelGeneral.EndTickBox.SelectedIndex = SoundComboIndex(_config.EndTickSound);

        // Général — Diagnostic (V8-H2).
        PanelGeneral.DiagLogBox.IsChecked = _config.DiagnosticsLogEnabled;
        var diagLogPath = new DiagLogService().LogPath;
        PanelGeneral.OpenDiagLogButton.IsEnabled = System.IO.File.Exists(diagLogPath);
        PanelGeneral.OpenDiagLogButton.Click += (_, _) => OpenDiagLog(diagLogPath);

        PreviewKeyDown += OnPreviewKeyDown;
        PanelGeneral.CopyLastButton.Click += (_, _) => CopyLastText();
        PanelGeneral.CheckUpdatesButton.Click += (_, _) => CheckModelUpdates();

        // Auto-enregistrement : chaque changement persiste immédiatement.
        PanelGeneral.SttCombo.SelectionChanged += (_, _) => Persist();
        PanelGeneral.SttCombo.LostFocus += (_, _) => Persist();           // saisie d'un id custom
        PanelGeneral.CleanupCombo.SelectionChanged += (_, _) => Persist();
        PanelGeneral.CleanupCombo.LostFocus += (_, _) => Persist();
        PanelGeneral.ModeBox.SelectionChanged += (_, _) => Persist();
        PanelGeneral.MaxDurationBox.SelectionChanged += (_, _) => Persist();
        PanelGeneral.MicBox.SelectionChanged += (_, _) => Persist();
        PanelGeneral.ThemeBox.SelectionChanged += (_, _) => Persist(); // applique le thème via onSaved
        PanelGeneral.StartupBox.Checked += (_, _) => Persist();
        PanelGeneral.StartupBox.Unchecked += (_, _) => Persist();
        PanelGeneral.TickSoundsBox.Checked += (_, _) => Persist();
        PanelGeneral.TickSoundsBox.Unchecked += (_, _) => Persist();
        PanelGeneral.StartTickBox.SelectionChanged += (_, _) => Persist();
        PanelGeneral.EndTickBox.SelectionChanged += (_, _) => Persist();
        PanelGeneral.DiagLogBox.Checked += (_, _) => Persist();
        PanelGeneral.DiagLogBox.Unchecked += (_, _) => Persist();
        PanelGeneral.AgentEnabledBox.Checked += (_, _) => Persist();
        PanelGeneral.AgentEnabledBox.Unchecked += (_, _) => Persist();
        PanelGeneral.AgentModelBox.SelectionChanged += (_, _) => Persist();
        PanelGeneral.AgentHotkeyButton.Click += (_, _) => StartAgentCapture();
        PanelGeneral.AgentHotkeyClearButton.Click += (_, _) => ClearAgentHotkey();
        PanelGeneral.ApiKeyBox.LostFocus += (_, _) => SaveApiKeyIfPresent();
        PanelGeneral.GroqKeyBox.LostFocus += (_, _) => SaveGroqKeyIfPresent();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturingDictation && !_capturingAgent) return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
                or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin)
        {
            // Touche modificatrice seule : Windows exige une touche normale en plus.
            var lbl = _capturingAgent ? PanelGeneral.AgentHotkeyError : PanelGeneral.DictationHotkeyError;
            lbl.Text = "Ajoutez une touche (lettre, chiffre, Espace, F1…) en plus des modificateurs.";
            return;
        }

        var mods = new List<string>();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) mods.Add("Ctrl");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) mods.Add("Alt");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) mods.Add("Maj");
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) mods.Add("Win");

        var candidate = new HotkeyConfig { Modifiers = mods, Key = key.ToString() };

        // Capture du raccourci de l'assistant vocal (V4-C).
        if (_capturingAgent)
        {
            _capturingAgent = false;
            if (!HotkeyParser.TryParse(candidate, out _, out _, out var aerr))
            {
                PanelGeneral.AgentHotkeyError.Text = aerr;
                RefreshAgentHotkeyButton();
                return;
            }
            if (_config.Hotkey is not null && SameHotkey(_config.Hotkey, candidate))
            {
                PanelGeneral.AgentHotkeyError.Text = "Ce raccourci est déjà utilisé par la dictée.";
                RefreshAgentHotkeyButton();
                return;
            }
            _config.AgentHotkey = candidate;
            RefreshAgentHotkeyButton();
            PanelGeneral.AgentHotkeyError.Text = "";
            Persist();
            return;
        }

        // Capture du raccourci de dictée.
        _capturingDictation = false;
        if (!HotkeyParser.TryParse(candidate, out _, out _, out var derr))
        {
            PanelGeneral.DictationHotkeyError.Text = derr;
            RefreshDictationHotkeyButton();
            return;
        }
        // Conflit avec le raccourci de l'assistant vocal ?
        if (_config.AgentHotkey is not null && SameHotkey(_config.AgentHotkey, candidate))
        {
            PanelGeneral.DictationHotkeyError.Text = "Ce raccourci est déjà utilisé par l'assistant vocal.";
            RefreshDictationHotkeyButton();
            return;
        }
        _config.Hotkey = candidate;
        RefreshDictationHotkeyButton();
        PanelGeneral.DictationHotkeyError.Text = "";
        Persist();
        return;
    }

    private void RefreshDictationHotkeyButton()
        => PanelGeneral.DictationHotkeyButton.Content = HotkeyParser.Describe(_config.Hotkey);

    private void StartDictationCapture()
    {
        _capturingDictation = true;
        PanelGeneral.DictationHotkeyButton.Content = "Appuyez sur la combinaison…";
        PanelGeneral.DictationHotkeyError.Text = "";
    }

    // --- Raccourci de l'assistant vocal (V4-C) ---
    private void RefreshAgentHotkeyButton()
        => PanelGeneral.AgentHotkeyButton.Content = _config.AgentHotkey is null ? "Aucun" : HotkeyParser.Describe(_config.AgentHotkey);

    private void StartAgentCapture()
    {
        _capturingAgent = true;
        PanelGeneral.AgentHotkeyButton.Content = "Appuyez sur la combinaison…";
        PanelGeneral.AgentHotkeyError.Text = "";
    }

    private void ClearAgentHotkey()
    {
        _capturingAgent = false;
        _config.AgentHotkey = null;
        RefreshAgentHotkeyButton();
        PanelGeneral.AgentHotkeyError.Text = "";
        Persist(); // le raccourci agent vit dans _config (pas dans un brouillon) → persister tout de suite
    }

    private static bool SameHotkey(HotkeyConfig a, HotkeyConfig b)
        => HotkeyParser.TryParse(a, out var ma, out var va, out _)
        && HotkeyParser.TryParse(b, out var mb, out var vb, out _)
        && ma == mb && va == vb;

    /// <summary>Affiche sous le modèle de nettoyage la clé qui lui manque (ex. modèle OpenRouter choisi
    /// sans clé OpenRouter) ; masqué dès que le modèle est utilisable. Rappelé à chaque sauvegarde.</summary>
    private void RefreshCleanupKeyHint()
    {
        var hint = _configService.CleanupKeyHint(_config, _config.CleanupModel);
        PanelGeneral.CleanupKeyHint.Text = hint ?? "";
        PanelGeneral.CleanupKeyHint.Visibility = hint is null ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Enregistre la nouvelle clé seulement si saisie ; bascule alors en état « configurée ».</summary>
    private void SaveApiKeyIfPresent()
    {
        if (!_loaded) return;
        if (PanelGeneral.ApiKeyEditPanel.Visibility != Visibility.Visible) return;
        if (string.IsNullOrWhiteSpace(PanelGeneral.ApiKeyBox.Password)) return; // champ vide : on garde la clé existante
        Persist();
        PanelGeneral.ApiKeyBox.Clear();
        SetApiKeyEditing(false); // affiche « Clé configurée ✓ »
    }

    /// <summary>Bascule l'affichage de la clé entre « configurée » et « édition ».</summary>
    private void SetApiKeyEditing(bool editing)
    {
        PanelGeneral.ApiKeyEditPanel.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        PanelGeneral.ApiKeySetPanel.Visibility  = editing ? Visibility.Collapsed : Visibility.Visible;
        // « Annuler » seulement si une clé existe déjà (sinon pas d'état configurée où revenir).
        PanelGeneral.ApiKeyCancelButton.Visibility =
            (editing && !string.IsNullOrWhiteSpace(_config.OpenRouterApiKey)) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Enregistre la clé Groq seulement si saisie ; bascule alors en état « configurée ».</summary>
    private void SaveGroqKeyIfPresent()
    {
        if (!_loaded) return;
        if (PanelGeneral.GroqKeyEditPanel.Visibility != Visibility.Visible) return;
        if (string.IsNullOrWhiteSpace(PanelGeneral.GroqKeyBox.Password)) return; // champ vide : on garde la clé existante
        Persist();
        PanelGeneral.GroqKeyBox.Clear();
        SetGroqKeyEditing(false);
    }

    /// <summary>Bascule l'affichage de la clé Groq entre « configurée » et « édition ».</summary>
    private void SetGroqKeyEditing(bool editing)
    {
        PanelGeneral.GroqKeyEditPanel.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
        PanelGeneral.GroqKeySetPanel.Visibility  = editing ? Visibility.Collapsed : Visibility.Visible;
        PanelGeneral.GroqKeyCancelButton.Visibility =
            (editing && !string.IsNullOrWhiteSpace(_config.GroqApiKey)) ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Ouvre le journal de diagnostic avec l'application associée (V8-H2, sans crash si absente).</summary>
    private static void OpenDiagLog(string path)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { /* pas d'application associée aux .log : on n'interrompt rien */ }
    }

    private void CopyLastText()
    {
        var t = _getLastText();
        if (string.IsNullOrEmpty(t)) { ShowCopyFeedback("Aucune dictée"); return; }
        try
        {
            System.Windows.Clipboard.SetText(t);
            ShowCopyFeedback("Copié ✓");
        }
        catch { ShowCopyFeedback("Échec de la copie"); } // presse-papiers verrouillé : pas de crash
    }

    /// <summary>Affiche un message bref sur le bouton, puis restaure son libellé (timer unique réutilisé).</summary>
    private void ShowCopyFeedback(string message)
    {
        PanelGeneral.CopyLastButton.Content = message;
        _copyTimer?.Stop();
        _copyTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
        _copyTimer.Tick += (_, _) => { _copyTimer!.Stop(); PanelGeneral.CopyLastButton.Content = "Copier le dernier texte"; };
        _copyTimer.Start();
    }

    // Client réseau partagé pour la vérification des modèles : appel UNIQUEMENT au clic, jamais au démarrage.
    private static readonly System.Net.Http.HttpClient _updateHttp = new();

    /// <summary>Vérifie chez Groq s'il existe une version plus récente des modèles utilisés (STT + nettoyage).</summary>
    private async void CheckModelUpdates()
    {
        var groqKey = _configService.ResolveGroqKey(_config);
        if (string.IsNullOrWhiteSpace(groqKey))
        {
            ShowUpdatesStatus("Renseigne ta clé Groq pour vérifier les modèles.");
            return;
        }

        PanelGeneral.CheckUpdatesButton.IsEnabled = false;
        var label = PanelGeneral.CheckUpdatesButton.Content;
        PanelGeneral.CheckUpdatesButton.Content = "Vérification…";
        PanelGeneral.UpdatesPanel.Children.Clear();
        PanelGeneral.UpdatesStatus.Visibility = Visibility.Collapsed;

        try
        {
            var ids = await GroqModelsClient.FetchModelIdsAsync(
                _updateHttp, groqKey, System.Threading.CancellationToken.None, TimeSpan.FromSeconds(15));

            var sttNew = ModelUpdateService.FindNewer(_config.SttModel, ids);
            var cleanupNew = ModelUpdateService.FindNewer(_config.CleanupModel, ids);

            if (sttNew.Count > 0) AddUpdateRow("Transcription", sttNew[0], isStt: true);
            if (cleanupNew.Count > 0) AddUpdateRow("Nettoyage", cleanupNew[0], isStt: false);
            if (sttNew.Count == 0 && cleanupNew.Count == 0) ShowUpdatesStatus("Tu es à jour ✓");
        }
        catch (OpenRouterException ex) { ShowUpdatesStatus(ex.Message); }
        catch { ShowUpdatesStatus("Vérification impossible pour le moment."); }
        finally
        {
            PanelGeneral.CheckUpdatesButton.IsEnabled = true;
            PanelGeneral.CheckUpdatesButton.Content = label;
        }
    }

    /// <summary>Ligne « &lt;type&gt; : &lt;nouvel id&gt; » + bouton « Mettre à jour » (bascule le modèle en un clic).</summary>
    private void AddUpdateRow(string kind, string newId, bool isStt)
    {
        var row = new System.Windows.Controls.Grid { Margin = new Thickness(2, 6, 0, 0) };
        row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new System.Windows.Controls.ColumnDefinition { Width = GridLength.Auto });

        var text = new System.Windows.Controls.TextBlock
        {
            Text = $"{kind} : {newId}",
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (System.Windows.Media.Brush)FindResource("InkBrush")
        };
        System.Windows.Controls.Grid.SetColumn(text, 0);

        var btn = new System.Windows.Controls.Button
        {
            Content = "Mettre à jour",
            Height = 28,
            Margin = new Thickness(10, 0, 0, 0),
            Padding = new Thickness(12, 0, 12, 0),
            Style = (Style)FindResource("SecondaryButton")
        };
        System.Windows.Controls.Grid.SetColumn(btn, 1);
        btn.Click += (_, _) => ApplyModelUpdate(newId, isStt, row);

        row.Children.Add(text);
        row.Children.Add(btn);
        PanelGeneral.UpdatesPanel.Children.Add(row);
    }

    /// <summary>Bascule le modèle (même écriture que le menu déroulant), persiste, et retire la ligne.</summary>
    private void ApplyModelUpdate(string newId, bool isStt, UIElement row)
    {
        if (isStt) SetComboModel(PanelGeneral.SttCombo, newId, ModelCatalog.Stt);
        else SetComboModel(PanelGeneral.CleanupCombo, newId, ModelCatalog.Cleanup);
        Persist(); // relit l'id depuis le combo (custom) → config.SttModel / CleanupModel
        PanelGeneral.UpdatesPanel.Children.Remove(row);
        ShowUpdatesStatus("Modèle mis à jour ✓");
    }

    private void ShowUpdatesStatus(string message)
    {
        PanelGeneral.UpdatesStatus.Text = message;
        PanelGeneral.UpdatesStatus.Visibility = Visibility.Visible;
    }

    /// <summary>Durées d'écoute proposées (s) ; l'ordre suit les items du menu « Durée max ».</summary>
    private static readonly int[] MaxDurations = { 30, 60, 120, 300 };

    /// <summary>Index du menu → secondes (repli sur 60 s si index hors plage).</summary>
    private static int IndexToMaxSeconds(int index)
        => index >= 0 && index < MaxDurations.Length ? MaxDurations[index] : 60;

    /// <summary>Secondes → index du menu (repli sur 60 s si valeur non proposée).</summary>
    private static int MaxSecondsToIndex(int seconds)
    {
        var idx = System.Array.IndexOf(MaxDurations, seconds);
        return idx >= 0 ? idx : 1; // 1 = 60 s
    }

    /// <summary>Relit les combos de modèles depuis la config. Nécessaire parce que l'onboarding (« Revoir la
    /// prise en main », ouvert PAR-DESSUS les Réglages) partage la même instance d'AppConfig et peut basculer
    /// CleanupModel vers un modèle Groq gratuit. Sans ce rafraîchissement, le combo resté périmé réécrasait la
    /// bascule au prochain Persist() — n'importe quel changement de réglage rebloquait alors la dictée.</summary>
    public void RefreshModelCombos()
    {
        bool wasLoaded = _loaded;
        _loaded = false; // SelectedItem déclenche SelectionChanged → Persist() : parasite ici
        try
        {
            SetComboModel(PanelGeneral.SttCombo, _config.SttModel, ModelCatalog.Stt);
            SetComboModel(PanelGeneral.CleanupCombo, _config.CleanupModel, ModelCatalog.Cleanup);
        }
        finally { _loaded = wasLoaded; }
        RefreshCleanupKeyHint(); // la clé demandée dépend du modèle de nettoyage, qui a pu changer
    }

    /// <summary>Pré-sélectionne le modèle connu, ou affiche l'id custom dans le champ éditable.</summary>
    private static void SetComboModel(System.Windows.Controls.ComboBox combo,
        string currentId, IReadOnlyList<ModelInfo> catalog)
    {
        var match = catalog.FirstOrDefault(m => m.Id == currentId);
        if (match is not null) combo.SelectedItem = match; // affiche le DisplayName (TextSearch.TextPath)
        else combo.Text = currentId;                       // id personnalisé saisi
    }

    /// <summary>Résout l'id à enregistrer : nom connu → id, sinon texte libre (custom), sinon repli.</summary>
    private static string ResolveComboModel(System.Windows.Controls.ComboBox combo,
        IReadOnlyList<ModelInfo> catalog, string fallback)
    {
        var t = (combo.Text ?? "").Trim();
        var byName = catalog.FirstOrDefault(m => m.DisplayName == t);
        if (byName is not null) return byName.Id;
        var byId = catalog.FirstOrDefault(m => m.Id == t);
        if (byId is not null) return byId.Id;
        return t.Length > 0 ? t : fallback;
    }

    // --- Choix du modèle IA (styles + assistant vocal) : « Par défaut (nettoyage) » + catalogue de nettoyage ---
    private static void PopulateModelCombo(System.Windows.Controls.ComboBox combo)
    {
        combo.Items.Clear();
        combo.Items.Add("Par défaut (modèle de nettoyage)");
        foreach (var m in ModelCatalog.Cleanup) combo.Items.Add(m.DisplayName);
    }

    /// <summary>Index du combo pour un id de modèle : 0 = défaut ; sinon position dans le catalogue (+1). Id inconnu → défaut.</summary>
    private static int ModelComboIndex(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return 0;
        for (int i = 0; i < ModelCatalog.Cleanup.Count; i++)
            if (ModelCatalog.Cleanup[i].Id == id) return i + 1;
        return 0;
    }

    /// <summary>Id de modèle depuis l'index du combo : 0 (défaut) → null ; sinon l'id du catalogue.</summary>
    private static string? ModelComboId(int index)
        => (index <= 0 || index > ModelCatalog.Cleanup.Count) ? null : ModelCatalog.Cleanup[index - 1].Id;

    // --- Choix du son (tops début/fin d'écoute, V8-K) ---
    private static void PopulateSoundCombo(System.Windows.Controls.ComboBox combo)
    {
        combo.Items.Clear();
        foreach (var s in SoundCatalog.All) combo.Items.Add(s.DisplayName);
    }

    /// <summary>Index du combo pour un id de son. Id absent/inconnu → 0 (« Aucun »).</summary>
    private static int SoundComboIndex(string? id)
    {
        for (int i = 0; i < SoundCatalog.All.Count; i++)
            if (SoundCatalog.All[i].Id == id) return i;
        return 0;
    }

    /// <summary>Id de son depuis l'index du combo. Index hors bornes → <see cref="SoundCatalog.None"/>.</summary>
    private static string SoundComboId(int index)
        => (index < 0 || index >= SoundCatalog.All.Count) ? SoundCatalog.None : SoundCatalog.All[index].Id;
}
