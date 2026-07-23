using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class Win32ScreensTests
{
    [Fact]
    public void ScreenInfo_CalculeRightEtBottom()
    {
        var s = new ScreenInfo(@"\\.\DISPLAY1", Left: -1920, Top: 0, Width: 1920, Height: 1040, IsPrimary: false);
        Assert.Equal(0, s.Right);
        Assert.Equal(1040, s.Bottom);
    }
}
