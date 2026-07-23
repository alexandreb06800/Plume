using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class DailyUsageServiceTests
{
    [Fact]
    public void Record_CreeEtIncremente()
    {
        var d = new Dictionary<string, DayUsage>();
        DailyUsageService.Record(d, new DateTime(2026, 7, 3), 100);
        DailyUsageService.Record(d, new DateTime(2026, 7, 3), 50);
        Assert.Equal(2, d["2026-07-03"].D);
        Assert.Equal(150, d["2026-07-03"].W);
    }

    [Fact]
    public void Sum_AdditionneLaPlage_BornesIncluses()
    {
        var d = new Dictionary<string, DayUsage>
        {
            ["2026-07-01"] = new() { D = 1, W = 10 },
            ["2026-07-03"] = new() { D = 2, W = 20 },
            ["2026-06-01"] = new() { D = 5, W = 50 },
        };
        var (dict, words) = DailyUsageService.Sum(d, new DateTime(2026, 7, 1), new DateTime(2026, 7, 3));
        Assert.Equal(3, dict);
        Assert.Equal(30, words);
    }

    [Fact]
    public void Prune_SupprimeAuDelaDe400Jours()
    {
        var d = new Dictionary<string, DayUsage>
        {
            ["2025-01-01"] = new() { D = 1, W = 1 },
            ["2026-07-01"] = new() { D = 1, W = 1 },
        };
        DailyUsageService.Prune(d, new DateTime(2026, 7, 3));
        Assert.False(d.ContainsKey("2025-01-01"));
        Assert.True(d.ContainsKey("2026-07-01"));
    }

    [Fact]
    public void Sum_ClesIllisibles_Ignorees()
    {
        var d = new Dictionary<string, DayUsage> { ["n'importe quoi"] = new() { D = 9, W = 9 } };
        var (dict, words) = DailyUsageService.Sum(d, DateTime.MinValue, DateTime.MaxValue);
        Assert.Equal(0, dict);
        Assert.Equal(0, words);
    }
}
