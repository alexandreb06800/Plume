using System.Text;
using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class WavUtilsTests
{
    [Fact]
    public void WriteWav_ProduitEnteteRIFFValide()
    {
        var pcm = new byte[1600 * 2]; // 0,1 s à 16 kHz 16-bit
        var wav = WavUtils.WriteWav(pcm, 16000, 16, 1);

        Assert.Equal("RIFF", Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", Encoding.ASCII.GetString(wav, 8, 4));
        Assert.Equal("fmt ", Encoding.ASCII.GetString(wav, 12, 4));
        Assert.Equal("data", Encoding.ASCII.GetString(wav, 36, 4));
        Assert.Equal(44 + pcm.Length, wav.Length);
    }

    [Fact]
    public void IsSilent_PcmNul_RetourneVrai()
    {
        var pcm = new byte[3200]; // que des zéros
        Assert.True(WavUtils.IsSilent(pcm));
    }

    [Fact]
    public void IsSilent_PcmFort_RetourneFaux()
    {
        var pcm = new byte[3200];
        for (int i = 0; i < pcm.Length; i += 2)
        {
            // échantillon proche du max (~32000)
            pcm[i] = 0x00; pcm[i + 1] = 0x7D;
        }
        Assert.False(WavUtils.IsSilent(pcm));
    }
}
