using System.IO;
using NAudio.Wave;

namespace Plume.Services;

/// <summary>Capture micro 16 kHz mono 16-bit en mémoire (NAudio), avec niveau et garde-fou durée.</summary>
public class AudioCaptureService : IDisposable
{
    /// <summary>Durée max par défaut (s) si la config n'en fournit pas.</summary>
    public const int DefaultMaxSeconds = 60;
    private const int SampleRate = 16000;
    private const int Bits = 16;
    private const int Channels = 1;

    private WaveInEvent? _waveIn;
    private MemoryStream? _buffer;
    private bool _maxReached;
    private bool _startedNotified;
    private int _maxSeconds = DefaultMaxSeconds; // durée max effective de la capture en cours
    // Signalé quand le thread d'enregistrement NAudio est sorti de sa boucle (arrêt volontaire OU micro perdu) :
    // aucune libération avant ce signal (voir StopAndWait).
    private ManualResetEventSlim? _threadDone;
    // Signalé au premier bloc audio : le thread d'enregistrement tourne réellement (voir StopAndWait).
    private ManualResetEventSlim? _threadStarted;

    public event Action<float>? LevelChanged;
    public event Action? MaxDurationReached;
    /// <summary>Émis au tout premier bloc audio reçu (micro réellement prêt).</summary>
    public event Action? CaptureStarted;
    /// <summary>Émis si le périphérique disparaît PENDANT la capture (débranché, casque Bluetooth éteint…),
    /// par opposition à un arrêt volontaire via <see cref="Stop"/> (qui désabonne avant d'arrêter).</summary>
    public event Action? DeviceLost;

    public bool TryStart(string? deviceName, int maxSeconds, out string? error)
    {
        error = null;
        // Défensif : ferme toute capture déjà ouverte avant d'en démarrer une nouvelle. Ne devrait jamais
        // arriver en usage normal, mais protège contre un micro orphelin si un appelant déclenchait deux
        // ouvertures concurrentes (le contrôleur interdit ce cas de son côté, cf. DictationController).
        Cleanup();
        try
        {
            _maxSeconds = maxSeconds > 0 ? maxSeconds : DefaultMaxSeconds;
            _maxReached = false;
            _startedNotified = false;
            _buffer = new MemoryStream();
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(SampleRate, Bits, Channels),
                DeviceNumber = ResolveDevice(deviceName),
                BufferMilliseconds = 40 // défaut NAudio 100 ms : buffers plus courts = 1er buffer (CaptureStarted) ~2x plus tôt
            };
            var done = _threadDone = new ManualResetEventSlim(false);
            _threadStarted = new ManualResetEventSlim(false);
            _waveIn.DataAvailable += OnData;
            // Jamais désabonné : suit le thread jusqu'au bout. Protégé : après une attente expirée, l'événement a
            // pu être libéré — une exception ici, sur le thread NAudio, tuerait le process.
            _waveIn.RecordingStopped += (_, _) => { try { done.Set(); } catch { } };
            _waveIn.RecordingStopped += OnRecordingStopped;
            _waveIn.StartRecording();
            return true;
        }
        catch
        {
            error = "Micro indisponible.";
            Cleanup();
            return false;
        }
    }

    /// <summary>NAudio signale ainsi la mort du périphérique en cours de capture (exception non nulle).
    /// Un arrêt volontaire (<see cref="Stop"/>) désabonne cet évènement avant d'arrêter : il ne peut donc
    /// se déclencher ici qu'en cas de perte réelle du micro.</summary>
    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        if (e.Exception is not null) DeviceLost?.Invoke();
    }

    /// <summary>Noms des périphériques d'entrée disponibles (pour les réglages).</summary>
    public static List<string> ListInputDevices()
    {
        var list = new List<string>();
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            try { list.Add(WaveInEvent.GetCapabilities(i).ProductName); } catch { }
        }
        return list;
    }

    private static int ResolveDevice(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0; // périphérique par défaut
        for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            if (WaveInEvent.GetCapabilities(i).ProductName == name) return i;
        return 0;
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        if (!_startedNotified)
        {
            _startedNotified = true;
            try { _threadStarted?.Set(); } catch { }
            CaptureStarted?.Invoke(); // micro prêt → la pastille passe au vert
        }

        _buffer?.Write(e.Buffer, 0, e.BytesRecorded);

        // Niveau pour le vu-mètre (max normalisé sur ce bloc).
        short peak = 0;
        for (int i = 0; i + 1 < e.BytesRecorded; i += 2)
        {
            short s = (short)(e.Buffer[i] | (e.Buffer[i + 1] << 8));
            short abs = s == short.MinValue ? short.MaxValue : Math.Abs(s);
            if (abs > peak) peak = abs;
        }
        LevelChanged?.Invoke(peak / 32768f);

        // Garde-fou durée : on ne notifie qu'une fois.
        if (!_maxReached && _buffer is not null
            && _buffer.Length >= (long)SampleRate * Channels * (Bits / 8) * _maxSeconds)
        {
            _maxReached = true;
            MaxDurationReached?.Invoke();
        }
    }

    /// <summary>Arrête la capture et retourne le WAV (vide si silence/aucun son). Ne lève jamais :
    /// un micro débranché en pleine dictée (USB, Bluetooth) rend un tableau vide, pas une exception.</summary>
    public byte[] Stop()
    {
        if (_waveIn is null || _buffer is null) return Array.Empty<byte>();
        // Désabonner AVANT StopRecording : un arrêt volontaire ne doit jamais se traduire par DeviceLost.
        try { _waveIn.RecordingStopped -= OnRecordingStopped; } catch { }
        StopAndWait(_waveIn, _threadStarted, _threadDone);
        try { _waveIn.DataAvailable -= OnData; } catch { }

        byte[] pcm;
        try { pcm = _buffer.ToArray(); } catch { pcm = Array.Empty<byte>(); }
        Cleanup();

        try
        {
            if (pcm.Length == 0 || WavUtils.IsSilent(pcm)) return Array.Empty<byte>();
            return WavUtils.WriteWav(pcm, SampleRate, Bits, Channels);
        }
        catch { return Array.Empty<byte>(); }
    }

    /// <summary>Délai max d'attente de la fin du thread d'enregistrement NAudio (mesuré : quelques ms).</summary>
    private const int StopWaitMs = 500;

    /// <summary>Délai max d'attente du démarrage effectif de la capture (1er bloc : ~40-90 ms mesurés).</summary>
    private const int StartWaitMs = 300;

    /// <summary>Arrête la capture et ATTEND que le thread d'enregistrement de NAudio soit sorti de sa boucle
    /// (événement RecordingStopped) avant toute libération. StopRecording ne fait que signaler l'arrêt : libérer
    /// aussitôt (Dispose) laissait le thread remettre en service un tampon déjà libéré (WaveInBuffer.Reuse →
    /// waveInPrepareHeader) → AccessViolationException qui tue le process — plantage du 2026-09-15, reproduit
    /// 4 fois sur 5 par 300 démarrages/arrêts rapides. RecordingStopped est levé sur le thread d'enregistrement
    /// (WaveInEvent créé hors thread UI, sans SynchronizationContext) : attendre ici ne peut pas l'interbloquer.
    /// Si le thread est déjà sorti (micro perdu), le signal est déjà posé : aucune attente. Attente bornée : au
    /// pire, comportement d'avant. Ne lève jamais.
    /// Arrêt demandé avant le démarrage effectif du thread (2e appui pendant l'ouverture du micro) : NAudio
    /// repasse alors en capture en démarrant et ignore l'arrêt (attente expirée dans 1 arrêt précoce sur ~10
    /// mesuré) — on attend d'abord le premier bloc audio, borné à StartWaitMs.</summary>
    private static void StopAndWait(WaveInEvent waveIn, ManualResetEventSlim? threadStarted, ManualResetEventSlim? threadDone)
    {
        try { if (threadDone is { IsSet: false }) threadStarted?.Wait(StartWaitMs); } catch { }
        try { waveIn.StopRecording(); } catch { /* périphérique déjà retiré */ }
        try { threadDone?.Wait(StopWaitMs); } catch { }
    }

    private void Cleanup()
    {
        if (_waveIn is not null)
        {
            try { _waveIn.RecordingStopped -= OnRecordingStopped; } catch { }
            // Capture encore active (annulation, fermeture de l'app) : même course que Stop(). OnData reste abonné
            // pendant l'attente (il signale le démarrage effectif), désabonné juste après.
            StopAndWait(_waveIn, _threadStarted, _threadDone);
            try { _waveIn.DataAvailable -= OnData; } catch { }
            try { _waveIn.Dispose(); } catch { /* périphérique déjà retiré : rien à libérer */ }
            _waveIn = null;
        }
        try { _threadDone?.Dispose(); } catch { }
        _threadDone = null;
        try { _threadStarted?.Dispose(); } catch { }
        _threadStarted = null;
        try { _buffer?.Dispose(); } catch { }
        _buffer = null;
    }

    public void Dispose() => Cleanup();
}
