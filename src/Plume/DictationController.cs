using System.Net.Http;
using System.Runtime.InteropServices;
using System.Windows;
using Plume.Models;
using Plume.Services;

namespace Plume;

/// <summary>État visuel de la dictée, affiché par la pastille flottante (unique retour visuel depuis V7).</summary>
public enum DictationVisual { Idle, Listening, Processing }

/// <summary>Orchestre le cycle dictée : capture → STT → nettoyage → insertion, avec pastille et erreurs.
/// Découpé en classes partielles par flux : <c>.Dictation.cs</c> (dictée classique), <c>.Styles.cs</c>
/// (reformulation/commande sur sélection), <c>.Agent.cs</c> (assistant vocal). Ce fichier porte l'état
/// partagé et le démarrage/arrêt de l'écoute, communs aux trois flux.</summary>
public partial class DictationController : IDisposable
{
    private enum State { Idle, Listening, Working }
    private enum Mode { Dictation, Agent, Command }

    // Délai du nettoyage (fixe). Le délai STT, lui, s'adapte à la taille de l'audio — voir
    // TranscriptionService.TimeoutFor — pour ne plus perdre une dictée longue sur un lien lent.
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(30);

    private readonly ConfigService _configService;
    private readonly Func<AppConfig> _getConfig;
    private readonly TrayIcon _tray;
    private readonly HotkeyService _hotkey;

    private readonly AudioCaptureService _audio = new();
    private readonly HttpClient _http;
    private readonly TranscriptionService _stt;
    private readonly CleanupService _cleanup;
    private readonly InsertionService _insertion = new();
    private readonly SelectionService _selection = new();
    private readonly AgentChatService _agent;
    private readonly DiagLogService _diagLog = new();

    private State _stateBacking = State.Idle;

    /// <summary>État courant. Le setter notifie <see cref="ListeningChanged"/> quand on entre/sort de l'écoute
    /// (utilisé par la pastille pour basculer en bouton Stop).</summary>
    private State _state
    {
        get => _stateBacking;
        set
        {
            bool wasListening = _stateBacking == State.Listening;
            _stateBacking = value;
            bool nowListening = value == State.Listening;
            if (wasListening != nowListening) ListeningChanged?.Invoke(nowListening);
        }
    }

    private CancellationTokenSource? _cts;
    private IntPtr _targetWindow; // fenêtre active au lancement de la dictée (cible du collage)
    private IntPtr _agentOrigin;  // fenêtre active au DÉBUT d'une conversation agent (cible du bouton « Insérer »)
    private SelectionStyle? _activeStyle; // style en cours (null = dictée)
    private uint _listeningVk;        // vk de la touche déclencheuse (pour le maintien enfoncé)
    private Mode _mode = Mode.Dictation;        // ce que StopAndProcess doit faire à l'arrêt de l'écoute
    private AgentSession? _session;             // conversation agent en cours (mémoire seule)
    private Views.AnswerWindow? _answerWindow;  // fenêtre de réponse (lazy, instance unique)
    private CapturedSelection? _commandSelection; // sélection capturée pour une « Commande vocale » (mode Command)

    // --- V8-G : ouverture du micro en arrière-plan (le spinner s'affiche AVANT, voir StartListening) ---
    private bool _micOpening;            // true pendant l'ouverture asynchrone du micro
    private bool _cancelMicOnOpen;       // Cancel() a été appelé pendant l'ouverture : fermer dès que prêt
    private bool _stopRequestedDuringOpen; // StopAndProcess() a été appelé pendant l'ouverture : traiter dès que prêt
    private System.Diagnostics.Stopwatch? _micStopwatch; // mesure appui → micro ouvert → premier buffer (debug)

    // Jeton de génération : incrémenté à chaque démarrage d'écoute et à chaque annulation. Les flux
    // asynchrones capturent leur numéro au départ et ne touchent plus l'état partagé si une session
    // plus récente a pris le relais (Cancel puis relance rapide pendant une opération en vol).
    private int _sessionId;

    /// <summary>Dernier texte inséré, conservé en mémoire seule (jamais persisté ni loggué).</summary>
    public string? LastText { get; private set; }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private System.Windows.Threading.DispatcherTimer? _releaseTimer;

    /// <summary>Permet à App d'ouvrir les réglages sur 401.</summary>
    public event Action? OpenSettingsRequested;

    /// <summary>Notifie l'entrée/sortie de l'état d'écoute (dictée ou style avec voix) : true = écoute en cours.
    /// Permet à la pastille de basculer en bouton Stop.</summary>
    public event Action<bool>? ListeningChanged;

    /// <summary>Notifie l'état visuel courant (Idle/Listening/Processing), reflété par la pastille flottante
    /// (unique retour visuel depuis la suppression de l'overlay en V7).</summary>
    public event Action<DictationVisual>? VisualChanged;
    private void SetVisual(DictationVisual v) => VisualChanged?.Invoke(v);

    /// <summary>Niveau vocal capté (0..1, pic brut par bloc audio) : anime les barres de la pastille pendant l'écoute.</summary>
    public event Action<float>? LevelChanged;

    public DictationController(ConfigService configService, Func<AppConfig> getConfig,
                               TrayIcon tray, HotkeyService hotkey)
    {
        _configService = configService;
        _getConfig = getConfig;
        _tray = tray;
        _hotkey = hotkey;

        // Timeout global volontairement infini : chaque appel impose son propre délai (voir TranscriptionService.TimeoutFor/CleanupTimeout),
        // ce qui évite qu'une transcription lente n'épuise le budget du nettoyage et donne un message d'erreur précis.
        _http = new HttpClient { Timeout = System.Threading.Timeout.InfiniteTimeSpan };
        _stt = new TranscriptionService(_http);
        _cleanup = new CleanupService(_http);
        _agent = new AgentChatService(_http);

        // Préchauffe les connexions Groq et OpenRouter en tâche de fond (DNS + TLS + pool) : tue le
        // « démarrage à froid » de la 1re dictée (STT comme nettoyage) sans bloquer l'ouverture.
        // Silencieux, sans effet sur la latence ressentie.
        _ = WarmUpAsync();

        // Micro prêt → pastille en état « Écoute » (barres vertes, animées selon le niveau capté).
        _audio.CaptureStarted += () => Application.Current.Dispatcher.Invoke(() =>
        {
            var micMs = _micStopwatch?.ElapsedMilliseconds;
            LogTiming($"Premier buffer audio : {micMs} ms");
            SetVisual(DictationVisual.Listening);
            var cfg = _getConfig();
            _ = _diagLog.Log(cfg.DiagnosticsLogEnabled, "Micro prêt", $"{micMs} ms");
            // Top sonore « micro prêt » (V8-K), tous modes d'écoute confondus.
            if (cfg.TickSoundsEnabled) TickSoundService.Play(cfg.StartTickSound);
        });
        _audio.LevelChanged += level => LevelChanged?.Invoke(level);
        // BeginInvoke (asynchrone) et non Invoke : l'événement arrive sur le thread de capture NAudio, et un
        // Invoke bloquant disposerait le WaveInEvent pendant que son DataAvailable s'exécute encore.
        _audio.MaxDurationReached += () => Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            // Avertissement sonore discret à la coupure automatique (durée max atteinte).
            try { System.Media.SystemSounds.Exclamation.Play(); } catch { /* son indisponible : on coupe quand même */ }
            StopAndProcess();
        }));
        // Micro disparu pendant l'écoute (débranché, casque Bluetooth éteint…) : sans ce retour, la pastille
        // resterait figée en écoute jusqu'à Échap ou la coupure durée max, sans qu'on sache pourquoi.
        _audio.DeviceLost += () => Application.Current.Dispatcher.Invoke(() =>
        {
            if (_state != State.Listening) return; // déjà arrêté/en traitement entre-temps : rien à faire
            Cancel();
            _tray.ShowBalloon("Plume", "Micro déconnecté.");
        });
        _hotkey.EscapePressed += () => Application.Current.Dispatcher.Invoke(Cancel);
    }

    /// <summary>Bascule la dictée globale (tray, raccourci, ou menu de la pastille). vk = touche déclencheuse
    /// (maintien). targetWindow, si renseigné (menu de la pastille), mémorise la fenêtre cible du collage
    /// au lieu de la déduire tardivement via GetForegroundWindow (évite une capture de la mauvaise fenêtre).</summary>
    public void ToggleDictation(uint vk = 0, IntPtr targetWindow = default)
    {
        bool hold = _getConfig().DictationMode == "hold";
        switch (_state)
        {
            case State.Idle:
                StartListening(Mode.Dictation, vk, null, targetWindow);
                if (_state == State.Listening && hold) StartReleasePolling();
                break;
            case State.Listening:
                // En MAINTIEN, l'arrêt se fait au relâchement de la touche (surveillance), PAS sur un nouvel
                // appui : Windows renvoie une auto-répétition du raccourci tant que la touche est tenue, et la
                // traiter comme « 2e appui » couperait l'écoute au bout de ~500 ms. On l'ignore donc en maintien.
                if (!hold && _mode == Mode.Dictation) StopAndProcess();
                break;
            case State.Working: break;
        }
    }

    /// <summary>Arrête l'écoute en cours (dictée ou style avec voix) et lance le traitement, comme un second
    /// appui sur le raccourci. Utilisé par la pastille quand elle affiche le bouton Stop.</summary>
    public void StopListening()
    {
        if (_state == State.Listening) StopAndProcess();
    }

    private bool _releaseArmed; // maintien : true une fois la touche vue physiquement enfoncée

    private void StartReleasePolling()
    {
        _releaseTimer?.Stop();
        _releaseArmed = false;
        _releaseTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(40)
        };
        _releaseTimer.Tick += (_, _) =>
        {
            var vk = (int)_listeningVk;
            // Aucune touche connue : pas d'arrêt automatique possible (on reste en écoute ; 2e appui pour arrêter).
            if (vk == 0) { _releaseTimer!.Stop(); return; }

            bool down = (GetAsyncKeyState(vk) & 0x8000) != 0;

            // On n'ARME la détection de relâchement qu'après avoir VU la touche enfoncée au moins une fois.
            // Juste après le WM_HOTKEY, l'état clavier peut encore renvoyer « relâchée » (course) : sans ce
            // garde-fou, le tout premier tick couperait l'écoute immédiatement.
            if (!_releaseArmed)
            {
                if (down) _releaseArmed = true;
                return;
            }

            // Armé + touche relâchée → fin du maintien : on arrête et on traite.
            if (!down)
            {
                _releaseTimer!.Stop();
                if (_state == State.Listening) StopAndProcess();
            }
        };
        _releaseTimer.Start();
    }

    public void Cancel()
    {
        if (_state == State.Idle) return;
        _sessionId++; // invalide tout flux asynchrone encore en vol (son finally ne touchera plus l'état)
        _releaseTimer?.Stop();
        _cts?.Cancel();
        // Micro encore en cours d'ouverture (thread d'arrière-plan) : ne pas y toucher depuis ce thread-ci
        // (accès concurrent à AudioCaptureService) — OnMicOpened fermera le micro dès qu'il sera prêt.
        if (_micOpening) _cancelMicOnOpen = true;
        else _audio.Stop();
        _hotkey.UnregisterEscape();
        // En mode Commande vocale, le presse-papiers contient encore le texte sélectionné :
        // restaurer le contenu d'origine avant de remettre l'état à Idle.
        if (_mode == Mode.Command && _commandSelection is not null)
        {
            _selection.RestoreClipboard(_commandSelection);
            _commandSelection = null;
        }
        SetVisual(DictationVisual.Idle);
        _state = State.Idle;
        _activeStyle = null;
    }

    private void StartListening(Mode mode = Mode.Dictation, uint vk = 0, SelectionStyle? style = null, IntPtr targetWindow = default)
    {
        // Une ouverture précédente est encore en cours (ex. Échap pendant l'ouverture d'un micro Bluetooth
        // lent : Cancel() remet _state à Idle avant que TryStart n'ait fini). Démarrer une seconde capture
        // ici la ferait concurremment à la première, qui deviendrait orpheline (micro jamais refermé).
        if (_micOpening) return;

        var cfg = _getConfig();
        // Clé exigée selon le flux réel : le modèle du style (Command) ou de l'assistant prime sur le global.
        var modelForKeys = mode switch
        {
            Mode.Agent => string.IsNullOrWhiteSpace(cfg.AgentModel) ? cfg.CleanupModel : cfg.AgentModel!,
            Mode.Command => string.IsNullOrWhiteSpace(style?.Model) ? cfg.CleanupModel : style!.Model!,
            _ => cfg.CleanupModel
        };
        if (_configService.MissingKeyMessage(cfg, modelForKeys) is string keyErr)
        {
            _tray.ShowBalloon("Plume", keyErr);
            return;
        }

        // Reconnexion Groq/OpenRouter si la précédente s'est éteinte (pool inactif > ~2 min) : cible surtout
        // les dictées qui suivent une pause, le préchauffage au démarrage (WarmUpAsync, constructeur) ne
        // couvrant que la toute première dictée après le lancement de l'app.
        _ = WarmUpAsync();

        _mode = mode;
        _activeStyle = style;
        _listeningVk = vk; // touche déclencheuse du raccourci de profil (0 si aucune)

        // Mémoriser la fenêtre où le curseur est, pour y revenir avant de coller (utile en dictée ; inoffensif en agent).
        // Si targetWindow est fournie (menu de la pastille : mémorisée au clic, avant l'activation du menu),
        // on l'utilise plutôt qu'un GetForegroundWindow tardif qui renverrait la pastille/le menu.
        // En mode Command, _targetWindow a déjà été mémorisée avant la capture de sélection (pastille en cours).
        if (mode != Mode.Command) _targetWindow = targetWindow != IntPtr.Zero ? targetWindow : GetForegroundWindow();
        // Mode agent : on fige la fenêtre d'origine au DÉBUT de la conversation seulement.
        // Les tours de suivi gardent cette cible (sinon GetForegroundWindow renverrait AnswerWindow, qui a le focus).
        if (mode == Mode.Agent && _session is null)
            _agentOrigin = GetForegroundWindow();

        // V8-G : le retour visuel (spinner) s'affiche AVANT d'ouvrir le micro — l'ouverture réelle (100-500 ms,
        // plus avec un casque Bluetooth ou au premier usage) était jusqu'ici entièrement ressentie comme de la
        // latence à l'appui. Le micro s'ouvre ensuite hors du thread UI ; voir OnMicOpened pour les courses
        // (annulation/2e appui pendant l'ouverture).
        SetVisual(DictationVisual.Processing); // spinner tant que le micro n'est pas prêt
        _hotkey.RegisterEscape(); // Échap annule pendant la dictée
        _state = State.Listening;
        _micOpening = true;
        _cancelMicOnOpen = false;
        _stopRequestedDuringOpen = false;
        _micStopwatch = System.Diagnostics.Stopwatch.StartNew();

        int session = ++_sessionId; // nouvelle session : les callbacks d'une ouverture précédente deviennent caducs
        Task.Run(() =>
        {
            bool ok = _audio.TryStart(cfg.InputDevice, cfg.MaxListeningSeconds, out var err);
            LogTiming($"Micro ouvert : {_micStopwatch?.ElapsedMilliseconds} ms (ok={ok})");
            Application.Current.Dispatcher.Invoke(() => OnMicOpened(ok, err, session));
        });
    }

    /// <summary>Suite de l'ouverture asynchrone du micro (thread UI). Gère les courses : annulation ou 2e appui
    /// survenus pendant l'ouverture (state encore Listening mais _waveIn pas encore prêt).</summary>
    private void OnMicOpened(bool ok, string? err, int session)
    {
        // Session périmée (Cancel puis relance pendant l'ouverture) : fermer ce micro-là sans toucher
        // à l'état de la session en cours (_state, _activeStyle...), déjà géré par Cancel(). En revanche,
        // _micOpening/_cancelMicOnOpen/_stopRequestedDuringOpen ne sont jamais touchés ailleurs : si on les
        // laissait tels quels ici, _micOpening resterait bloqué à true pour toujours et StartListening
        // refuserait toute nouvelle écoute (dictée morte jusqu'au redémarrage de l'app).
        if (session != _sessionId)
        {
            if (ok) _audio.Stop();
            _micOpening = false;
            _cancelMicOnOpen = false;
            _stopRequestedDuringOpen = false;
            return;
        }
        _micOpening = false;

        if (_cancelMicOnOpen)
        {
            _cancelMicOnOpen = false;
            _stopRequestedDuringOpen = false;
            if (ok) _audio.Stop(); // annulé entre-temps : fermer le micro qui vient de s'ouvrir, ne rien afficher
            return;
        }

        if (!ok)
        {
            _stopRequestedDuringOpen = false;
            _hotkey.UnregisterEscape();
            // En mode Commande vocale, la sélection a déjà été capturée (presse-papiers modifié) avant l'ouverture
            // du micro : la restaurer puisque l'écoute n'aura finalement pas lieu (cf. l'ancien contrôle
            // synchrone dans StartVoiceStyleAsync, devenu inopérant car _state passe à Listening avant ce résultat).
            if (_mode == Mode.Command && _commandSelection is not null)
            {
                _selection.RestoreClipboard(_commandSelection);
                _commandSelection = null;
            }
            SetVisual(DictationVisual.Idle);
            _state = State.Idle;
            _activeStyle = null;
            _tray.ShowBalloon("Plume", err ?? "Micro indisponible.");
            return;
        }

        if (_stopRequestedDuringOpen)
        {
            _stopRequestedDuringOpen = false;
            StopAndProcess();
        }
    }

    /// <summary>Ouvre en parallèle une connexion vers Groq (STT) et OpenRouter (nettoyage),
    /// requêtes légères ignorées, pour éviter le coût de connexion à froid sur la première
    /// dictée. Ne lève jamais.</summary>
    private async Task WarmUpAsync()
    {
        try
        {
            var cfg = _getConfig();
            await Task.WhenAll(
                WarmUpHostAsync("https://api.groq.com/openai/v1/models", _configService.ResolveGroqKey(cfg)),
                WarmUpHostAsync("https://openrouter.ai/api/v1/models", _configService.ResolveApiKey(cfg)));
        }
        catch { /* le préchauffage est best-effort : aucune conséquence en cas d'échec */ }
    }

    /// <summary>Préchauffe un hôte (DNS + TLS + pool du HttpClient partagé). Sans clé, ne fait rien.
    /// Timeout propre de 10 s. Ne lève jamais.</summary>
    private async Task WarmUpHostAsync(string url, string? apiKey)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(apiKey)) return; // pas de clé → rien à préchauffer
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var resp = await _http.SendAsync(req, cts.Token);
        }
        catch { /* le préchauffage est best-effort : aucune conséquence en cas d'échec */ }
    }

    // --- Diagnostic latence (temporaire) ---
    // Écrit les temps dans %TEMP%\plume-timing.log ET en sortie debug. Jamais de crash.
    private static readonly string TimingLogPath =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "plume-timing.log");

    [System.Diagnostics.Conditional("DEBUG")]
    private static void LogTiming(string message)
    {
        var line = $"[Plume] {DateTime.Now:HH:mm:ss} {message}";
        System.Diagnostics.Debug.WriteLine(line);
        try { System.IO.File.AppendAllText(TimingLogPath, line + Environment.NewLine); }
        catch { /* le diagnostic ne doit jamais perturber la dictée */ }
    }

    public void Dispose()
    {
        _audio.Dispose();
        _http.Dispose();
    }
}
