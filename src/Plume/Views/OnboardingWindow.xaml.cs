using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Plume.Models;
using Plume.Services;

namespace Plume.Views;

/// <summary>Assistant de prise en main (V8-B) : 3 étapes — coller la clé API Groq, tester le micro,
/// réussir la première dictée. Affiché au démarrage seulement si AUCUNE clé n'est configurée, ou à la
/// demande via À propos → « Revoir la prise en main ». Jamais instancié au démarrage normal
/// (RÈGLE PRIMORDIALE : coût nul quand une clé existe).</summary>
public partial class OnboardingWindow : Window
{
    private readonly ConfigService _configService;
    private readonly AppConfig _config;
    // Client réseau partagé : appel UNIQUEMENT au clic « Tester la clé », jamais au démarrage.
    private static readonly System.Net.Http.HttpClient _http = new();

    private AudioCaptureService? _mic;  // capture dédiée au vu-mètre (étape 2), indépendante de la dictée
    private float _smoothedLevel;
    private bool _micHeard;
    private bool _populatingDevices;
    private bool _keyOk;
    private int _step;                  // 0 = clé, 1 = micro, 2 = dictée

    public OnboardingWindow(ConfigService configService, AppConfig config)
    {
        InitializeComponent();
        AppLogo.Apply(this); // icône de fenêtre (barre des tâches / titre)
        _configService = configService;
        _config = config;

        // Barre de titre au thème, mise à jour à chaud (même motif qu'AnswerWindow).
        SourceInitialized += (_, _) =>
        {
            var theme = ((App)Application.Current).Theme;
            DarkTitleBar.Apply(this, theme?.CurrentDark ?? false);
            if (theme is not null)
            {
                Action<bool> onChanged = dark => Dispatcher.Invoke(() => DarkTitleBar.Apply(this, dark));
                theme.Changed += onChanged;
                Closed += (_, _) => theme.Changed -= onChanged;
            }
        };

        LaterButton.Click += (_, _) => Close(); // l'assistant reviendra au prochain démarrage tant qu'aucune clé n'existe
        NextButton.Click += (_, _) => OnNext();
        TestKeyButton.Click += (_, _) => _ = TestKeyAsync();
        GroqLink.Click += (_, _) => OpenUrl("https://console.groq.com/keys");
        OpenRouterLink.Click += (_, _) => OpenUrl("https://openrouter.ai/keys");
        OpenRouterToggle.Click += (_, _) =>
            OpenRouterPanel.Visibility = OpenRouterPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;
        OpenRouterBox.PasswordChanged += (_, _) => UpdateNext();
        DictationTarget.TextChanged += (_, _) => OnDictationTextChanged();
        MicDeviceBox.SelectionChanged += (_, _) => OnMicDeviceChanged();
        Closed += (_, _) => StopMic(); // jamais de micro laissé ouvert

        // « Revoir la prise en main » alors qu'une clé existe déjà : l'étape 1 est acquise d'office.
        if (!string.IsNullOrWhiteSpace(_config.GroqApiKey) || !string.IsNullOrWhiteSpace(_config.OpenRouterApiKey))
        {
            _keyOk = true;
            SetStatus(KeyStatus, "Une clé est déjà configurée ✓", ok: true);
        }
        ShowStep(0);
    }

    // ---------- Navigation ----------

    private void ShowStep(int step)
    {
        _step = step;
        StepKey.Visibility       = step == 0 ? Visibility.Visible : Visibility.Collapsed;
        StepMic.Visibility       = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        StepDictation.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        StepLabel.Text = step switch
        {
            0 => "Étape 1 sur 3 — Ta clé API",
            1 => "Étape 2 sur 3 — Ton micro",
            _ => "Étape 3 sur 3 — Ta première dictée"
        };
        NextButton.Content = step == 2 ? "Terminer" : "Continuer";
        UpdateNext();
    }

    private void OnNext()
    {
        switch (_step)
        {
            case 0:
                SaveOpenRouterKeyIfProvided();
                ShowStep(1);
                PopulateMicDevices();
                StartMicTest();
                break;
            case 1:
                StopMic();
                ShowStep(2);
                PrepareDictationStep();
                break;
            default:
                Close();
                break;
        }
    }

    /// <summary>Active « Continuer »/« Terminer » selon l'étape : clé validée (ou repli OpenRouter saisi)
    /// pour quitter l'étape 1 ; l'étape micro reste toujours franchissable (« Continuer quand même ») ;
    /// « Terminer » s'active dès qu'un texte a été dicté dans la zone.</summary>
    private void UpdateNext()
    {
        NextButton.IsEnabled = _step switch
        {
            0 => _keyOk || !string.IsNullOrWhiteSpace(OpenRouterBox.Password),
            1 => true,
            _ => !string.IsNullOrWhiteSpace(DictationTarget.Text)
        };
    }

    // ---------- Étape 1 : clé API ----------

    /// <summary>Teste la clé collée par un appel léger à la liste des modèles Groq (aucun token consommé),
    /// puis la sauvegarde (chiffrée par ConfigService). Échec → message clair, on peut réessayer.</summary>
    private async System.Threading.Tasks.Task TestKeyAsync()
    {
        var key = KeyBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            SetStatus(KeyStatus, "Colle d'abord ta clé dans le champ ci-dessus.", ok: false);
            return;
        }
        TestKeyButton.IsEnabled = false;
        var label = TestKeyButton.Content;
        TestKeyButton.Content = "Vérification…";
        try
        {
            await GroqModelsClient.FetchModelIdsAsync(_http, key,
                System.Threading.CancellationToken.None, TimeSpan.FromSeconds(15));
            _config.GroqApiKey = key;
            // Config 100 % Groq (aucune clé OpenRouter) : le modèle de nettoyage par défaut passe par
            // OpenRouter et bloquerait la première dictée de l'étape 3 — bascule sur le modèle Groq gratuit.
            _config.CleanupModel = ConfigService.CleanupModelUtilisable(
                _config.CleanupModel, _configService.ResolveApiKey(_config) is not null);
            try { _configService.Save(_config); } catch { /* disque indisponible : la clé reste en mémoire */ }
            _keyOk = true;
            SetStatus(KeyStatus, "Clé valide ✓ — enregistrée.", ok: true);
        }
        catch (OpenRouterException ex) { SetStatus(KeyStatus, ex.Message, ok: false); }
        catch { SetStatus(KeyStatus, "Vérification impossible pour le moment.", ok: false); }
        finally
        {
            TestKeyButton.IsEnabled = true;
            TestKeyButton.Content = label;
            UpdateNext();
        }
    }

    /// <summary>Repli : une clé OpenRouter collée est enregistrée au passage à l'étape suivante
    /// (pas d'appel de test équivalent gratuit chez OpenRouter — la clé sera validée à l'usage).</summary>
    private void SaveOpenRouterKeyIfProvided()
    {
        var key = OpenRouterBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(key)) return;
        _config.OpenRouterApiKey = key;
        try { _configService.Save(_config); } catch { /* disque indisponible : la clé reste en mémoire */ }
    }

    // ---------- Étape 2 : micro ----------

    /// <summary>Remplit le sélecteur de micro ; visible seulement s'il y a plusieurs périphériques (spec).</summary>
    private void PopulateMicDevices()
    {
        _populatingDevices = true;
        try
        {
            var devices = AudioCaptureService.ListInputDevices();
            MicDevicePanel.Visibility = devices.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
            MicDeviceBox.ItemsSource = devices;
            MicDeviceBox.SelectedItem = devices.FirstOrDefault(d => d == _config.InputDevice) ?? devices.FirstOrDefault();
        }
        finally { _populatingDevices = false; }
    }

    /// <summary>(Re)démarre la capture dédiée au vu-mètre. Instance séparée du contrôleur de dictée,
    /// arrêtée dès qu'on quitte l'étape ou la fenêtre (jamais de micro ouvert en arrière-plan).</summary>
    private void StartMicTest()
    {
        StopMic();
        _smoothedLevel = 0;
        _micHeard = false;
        _mic = new AudioCaptureService();
        _mic.LevelChanged += OnMicLevel;
        // Le test peut durer plus que la garde de durée : on relance silencieusement (l'audio est jeté).
        _mic.MaxDurationReached += () => Dispatcher.BeginInvoke(new Action(() => { if (_step == 1) StartMicTest(); }));
        if (!_mic.TryStart(MicDeviceBox.SelectedItem as string ?? _config.InputDevice, 120, out _))
        {
            _mic.Dispose();
            _mic = null;
            SetStatus(MicStatus, "Aucun micro détecté. Branche un micro et reviens dans les Réglages pour le choisir, "
                + "ou continue quand même.", ok: false);
            NextButton.Content = "Continuer quand même";
            return;
        }
        SetStatus(MicStatus, "Parle normalement : la barre doit bouger.", ok: null);
    }

    /// <summary>Niveau reçu sur le thread de capture NAudio → marshalé UI. Lissage et gain identiques
    /// aux barres de la pastille (BadgeWindow.SetLevel) pour un rendu cohérent.</summary>
    private void OnMicLevel(float level) => Dispatcher.BeginInvoke(new Action(() =>
    {
        _smoothedLevel = _smoothedLevel * 0.55f + level * 0.45f;
        var amount = Math.Clamp(_smoothedLevel * 3.2, 0, 1);
        LevelFill.Width = amount * LevelTrack.ActualWidth;
        if (!_micHeard && _smoothedLevel > 0.02f)
        {
            _micHeard = true;
            SetStatus(MicStatus, "Ton micro fonctionne ✓", ok: true);
        }
    }));

    /// <summary>Changement de micro dans le sélecteur : mémorisé dans la config (comme dans les Réglages)
    /// et la capture repart sur ce périphérique.</summary>
    private void OnMicDeviceChanged()
    {
        if (_populatingDevices || _step != 1) return;
        _config.InputDevice = MicDeviceBox.SelectedItem as string;
        try { _configService.Save(_config); } catch { }
        StartMicTest();
    }

    private void StopMic()
    {
        if (_mic is null) return;
        _mic.LevelChanged -= OnMicLevel;
        _mic.Stop();   // l'audio du test est jeté : rien n'est transcrit ni envoyé
        _mic.Dispose();
        _mic = null;
    }

    // ---------- Étape 3 : première dictée ----------

    private void PrepareDictationStep()
    {
        var raccourci = HotkeyParser.Describe(_config.Hotkey);
        DictationInstruction.Text =
            $"Appuie sur {raccourci}, dis « Bonjour, ceci est ma première dictée », "
            + $"puis appuie à nouveau sur {raccourci}. Le texte apparaîtra ci-dessous.";
        // Focus différé : la zone doit tenir le curseur pour recevoir l'insertion (collage au curseur).
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => DictationTarget.Focus()));
    }

    private void OnDictationTextChanged()
    {
        if (string.IsNullOrWhiteSpace(DictationTarget.Text)) return;
        DictationSuccess.Visibility = Visibility.Visible;
        UpdateNext();
    }

    // ---------- Aides ----------

    /// <summary>Statut coloré via les tokens du thème (SetResourceReference : suit le clair/sombre à chaud).
    /// ok = null → neutre, true → vert, false → erreur.</summary>
    private static void SetStatus(TextBlock block, string text, bool? ok)
    {
        block.Text = text;
        block.SetResourceReference(TextBlock.ForegroundProperty,
            ok is null ? "SecondaryBrush" : ok.Value ? "GreenBrush" : "ErrorBrush");
    }

    /// <summary>Ouvre une URL dans le navigateur par défaut, sans crash si aucun n'est associé.</summary>
    private static void OpenUrl(string url)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch { /* pas de navigateur associé : rien à faire */ }
    }
}
