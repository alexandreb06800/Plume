using Plume.Services;
using Xunit;

namespace Plume.Tests;

public class StarRatingTests
{
    [Theory]
    [InlineData(0, "☆☆☆☆☆")]
    [InlineData(3, "★★★☆☆")]
    [InlineData(5, "★★★★★")]
    public void Format_RenvoieCinqCaracteres(int filled, string expected)
        => Assert.Equal(expected, StarRating.Format(filled));

    [Theory]
    [InlineData(-2, "☆☆☆☆☆")]
    [InlineData(9, "★★★★★")]
    public void Format_BorneLesValeurs(int filled, string expected)
        => Assert.Equal(expected, StarRating.Format(filled));
}
