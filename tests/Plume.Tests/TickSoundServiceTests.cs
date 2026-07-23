using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class TickSoundServiceTests
{
    [Theory]
    [InlineData("discret", 30)]
    [InlineData("clair", 20)]
    public void GenerateWav_ProduitUnWavValideDeLaBonneDuree(string id, int durationMs)
    {
        var wav = TickSoundService.GenerateWav(id);

        Assert.True(wav.Length > 44); // en-tête WAV (44 octets) + données
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wav, 0, 4));
        Assert.Equal("WAVE", System.Text.Encoding.ASCII.GetString(wav, 8, 4));

        int expectedPcmBytes = 16000 * durationMs / 1000 * 2; // 16 kHz, 16 bits mono
        Assert.Equal(44 + expectedPcmBytes, wav.Length);
    }

    [Theory]
    [InlineData("")]
    [InlineData("id-inconnu")]
    public void GenerateWav_IdVideOuInconnu_RetourneTableauVide(string id)
    {
        var wav = TickSoundService.GenerateWav(id);
        Assert.Empty(wav);
    }

    [Fact]
    public void Play_IdVide_NeLevePas()
    {
        var ex = Record.Exception(() => TickSoundService.Play(""));
        Assert.Null(ex);
    }

    [Fact]
    public void Play_IdConnu_NeLevePas()
    {
        // Sur une machine sans périphérique audio, SoundPlayer peut échouer : le service doit
        // avaler l'erreur silencieusement (RÈGLE PRIMORDIALE, jamais de crash).
        var ex = Record.Exception(() => TickSoundService.Play("discret"));
        Assert.Null(ex);
    }
}
