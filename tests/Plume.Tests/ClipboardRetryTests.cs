using Plume.Models;
using Xunit;

namespace Plume.Tests;

public class ClipboardRetryTests
{
    [Fact]
    public void WithRetry_SuccesImmediat_UneSeuleExecution()
    {
        int appels = 0;
        ClipboardSnapshot.WithRetry(() => appels++);
        Assert.Equal(1, appels);
    }

    [Fact]
    public void WithRetry_EchecPuisSucces_ReessaieEtReussit()
    {
        int appels = 0;
        ClipboardSnapshot.WithRetry(() =>
        {
            appels++;
            if (appels < 3) throw new InvalidOperationException("presse-papiers occupé");
        });
        Assert.Equal(3, appels);
    }

    [Fact]
    public void WithRetry_EchecPersistant_RelaieLaDerniereException()
    {
        int appels = 0;
        Assert.Throws<InvalidOperationException>(() =>
            ClipboardSnapshot.WithRetry(() => { appels++; throw new InvalidOperationException(); }));
        Assert.Equal(3, appels);
    }
}
