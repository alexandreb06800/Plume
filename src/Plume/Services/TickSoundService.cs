using System.IO;
using NAudio.Wave;

namespace Plume.Services;

/// <summary>Génère et joue de courts tons synthétiques pour les tops début/fin d'écoute (V8-K).
/// Aucun fichier audio : la forme d'onde est calculée en code. Jamais de crash : périphérique
/// audio absent ou indisponible → échec silencieux, la dictée n'est jamais affectée.</summary>
public static class TickSoundService
{
    private const int SampleRate = 16000;

    /// <summary>Génère le WAV (16 kHz, 16 bits, mono) du son <paramref name="id"/> : une sinusoïde
    /// courte avec enveloppe fade-in/fade-out (évite tout clic). Retourne un tableau vide si
    /// <paramref name="id"/> est <see cref="SoundCatalog.None"/> ou inconnu.</summary>
    public static byte[] GenerateWav(string id)
    {
        var (frequencyHz, durationMs, amplitude, fadeMs) = id switch
        {
            "discret" => (700.0, 30, 0.18, 6),
            "clair" => (1400.0, 20, 0.22, 4),
            _ => (0.0, 0, 0.0, 0),
        };
        if (durationMs == 0) return Array.Empty<byte>();

        int sampleCount = SampleRate * durationMs / 1000;
        int fadeSamples = Math.Max(1, SampleRate * fadeMs / 1000);
        var pcm = new byte[sampleCount * 2];
        for (int i = 0; i < sampleCount; i++)
        {
            double t = (double)i / SampleRate;
            double envelope = i < fadeSamples ? (double)i / fadeSamples
                : i >= sampleCount - fadeSamples ? (double)(sampleCount - i) / fadeSamples
                : 1.0;
            short sample = (short)(short.MaxValue * amplitude * envelope * Math.Sin(2 * Math.PI * frequencyHz * t));
            pcm[i * 2] = (byte)(sample & 0xFF);
            pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }
        return WavUtils.WriteWav(pcm, SampleRate, 16, 1);
    }

    // Instances actives retenues le temps de la lecture (évite un ramassage GC prématuré qui
    // couperait le son en silence), puis libérées via PlaybackStopped (évite toute fuite de
    // handles audio natifs). System.Media.SoundPlayer (essayé avant NAudio) s'est révélé peu
    // fiable pour des sons courts joués répétitivement (échecs silencieux, sans exception,
    // constatés en test réel) — comportement documenté de cette API .NET historique. NAudio
    // (déjà dépendance du projet pour le micro) utilise la lecture WinMM (waveOut) de NAudio, plus robuste.
    private static readonly List<(WaveOutEvent Output, WaveFileReader Reader)> _active = new();
    private static readonly object _lock = new();

    /// <summary>Joue le son <paramref name="id"/> de façon non bloquante. L'ouverture du
    /// périphérique audio (<see cref="WaveOutEvent.Init"/>) est synchrone et peut prendre
    /// quelques dizaines de ms : contrairement à l'ancien <c>SoundPlayer</c> (quasi instantané),
    /// tout le travail est donc délégué à un thread d'arrière-plan (<see cref="Task.Run"/>) pour
    /// rester hors du chemin critique de la dictée (RÈGLE PRIMORDIALE). Ne fait rien si
    /// <paramref name="id"/> est <see cref="SoundCatalog.None"/>/inconnu. Ne lève jamais
    /// (même en cas d'échec, l'exception reste contenue dans la tâche d'arrière-plan).</summary>
    public static void Play(string id)
    {
        _ = Task.Run(() =>
        {
            try
            {
                var wav = GenerateWav(id);
                if (wav.Length == 0) return;

                var reader = new WaveFileReader(new MemoryStream(wav));
                var output = new WaveOutEvent();
                output.Init(reader);
                lock (_lock) { _active.Add((output, reader)); }
                output.PlaybackStopped += (_, _) =>
                {
                    // Thread de lecture NAudio : une exception ici tuerait le process (même règle qu'AudioCaptureService).
                    try
                    {
                        lock (_lock) { _active.Remove((output, reader)); }
                        output.Dispose();
                        reader.Dispose();
                    }
                    catch { }
                };
                output.Play();
            }
            catch { /* périphérique audio indisponible ou autre échec : silencieux, jamais de crash */ }
        });
    }
}
