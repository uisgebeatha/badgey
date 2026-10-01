namespace Badgey.Wpf.Tests;

public class PreviewAnimationTimingTests
{
    [Theory]
    [InlineData(1, 833)]
    [InlineData(2, 769)]
    [InlineData(3, 500)]
    [InlineData(4, 417)]
    [InlineData(5, 357)]
    [InlineData(6, 222)]
    [InlineData(7, 133)]
    [InlineData(8, 67)]
    public void GetInterval_ReturnsReferenceMillisecondsPerFrame(
        int speed,
        int expectedMilliseconds)
    {
        Assert.Equal(
            TimeSpan.FromMilliseconds(expectedMilliseconds),
            PreviewAnimationTiming.GetInterval(speed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void GetInterval_RejectsInvalidSpeed(int speed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PreviewAnimationTiming.GetInterval(speed));
    }

    [Fact]
    public void GetInterval_SpeedEightIsFastestAndProgressionIsMonotonic()
    {
        var intervals = Enumerable.Range(1, 8)
            .Select(PreviewAnimationTiming.GetInterval)
            .ToArray();

        for (var index = 1; index < intervals.Length; index++)
        {
            Assert.True(intervals[index] < intervals[index - 1]);
        }
    }
}
