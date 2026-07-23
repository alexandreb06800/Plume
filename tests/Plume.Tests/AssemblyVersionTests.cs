using Xunit;

namespace Plume.Tests;

public class AssemblyVersionTests
{
    [Fact]
    public void Plume_EstEnVersion_5_3()
    {
        var v = typeof(Plume.Models.AppConfig).Assembly.GetName().Version!;
        Assert.Equal(5, v.Major);
        Assert.Equal(3, v.Minor);
    }
}
