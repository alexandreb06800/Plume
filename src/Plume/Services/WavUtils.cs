using System.IO;
using System.Text;

namespace Plume.Services;

/// <summary>Encodage WAV en mémoire et détection de silence.</summary>
public static class WavUtils
{
    public static byte[] WriteWav(byte[] pcm, int sampleRate, int bits, int channels)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.ASCII);
        int byteRate = sampleRate * channels * bits / 8;
        short blockAlign = (short)(channels * bits / 8);

        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + pcm.Length);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);                       // taille du sous-bloc fmt
        w.Write((short)1);                 // PCM
        w.Write((short)channels);
        w.Write(sampleRate);
        w.Write(byteRate);
        w.Write(blockAlign);
        w.Write((short)bits);
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(pcm.Length);
        w.Write(pcm);
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>Vrai si l'amplitude max (16-bit) normalisée est sous le seuil.</summary>
    public static bool IsSilent(byte[] pcm16, double threshold = 0.01)
        => (MaxAmplitude(pcm16) / 32768.0) < threshold;

    /// <summary>Amplitude crête absolue (0..32768) du PCM 16-bit.</summary>
    public static int MaxAmplitude(byte[] pcm16)
    {
        if (pcm16.Length < 2) return 0;
        short max = 0;
        for (int i = 0; i + 1 < pcm16.Length; i += 2)
        {
            short sample = (short)(pcm16[i] | (pcm16[i + 1] << 8));
            short abs = sample == short.MinValue ? short.MaxValue : Math.Abs(sample);
            if (abs > max) max = abs;
        }
        return max;
    }
}
