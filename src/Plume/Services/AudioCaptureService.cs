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
            _waveIn.DataAvailable += OnData;
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
        try { _waveIn.StopRecording(); } catch { }
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

    private void Cleanup()
    {
        if (_waveIn is not null)
        {
            try { _waveIn.DataAvailable -= OnData; } catch { }
            try { _waveIn.RecordingStopped -= OnRecordingStopped; } catch { }
            try { _waveIn.Dispose(); } catch { /* périphérique déjà retiré : rien à libérer */ }
            _waveIn = null;
        }
        try { _buffer?.Dispose(); } catch { }
        _buffer = null;
    }

    public void Dispose() => Cleanup();
}
