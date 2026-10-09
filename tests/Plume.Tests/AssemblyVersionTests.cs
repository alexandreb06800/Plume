using Xunit;

namespace Plume.Tests;

public class AssemblyVersionTests
{
    [Fact]
    public void Plume_EstEnVersion_5_4()
    {
        var v = typeof(Plume.Models.AppConfig).Assembly.GetName().Version!;
        Assert.Equal(5, v.Major);
        Assert.Equal(4, v.Minor);
    }
}
