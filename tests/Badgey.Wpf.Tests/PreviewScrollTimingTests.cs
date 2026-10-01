namespace Badgey.Wpf.Tests;

public class PreviewScrollTimingTests
{
    [Theory]
    [InlineData(1, 240)]
    [InlineData(2, 195)]
    [InlineData(3, 155)]
    [InlineData(4, 120)]
    [InlineData(5, 82)]
    [InlineData(6, 55)]
    [InlineData(7, 32)]
    [InlineData(8, 19)]
    public void GetInterval_ReturnsCalibratedMilliseconds(
        int speed,
        int expectedMilliseconds)
    {
        Assert.Equal(
            TimeSpan.FromMilliseconds(expectedMilliseconds),
            PreviewScrollTiming.GetInterval(speed));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void GetInterval_RejectsInvalidSpeed(int speed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PreviewScrollTiming.GetInterval(speed));
    }

    [Fact]
    public void GetInterval_SpeedEightIsFastestAndProgressionIsMonotonic()
    {
        var intervals = Enumerable.Range(1, 8)
            .Select(PreviewScrollTiming.GetInterval)
            .ToArray();

        for (var index = 1; index < intervals.Length; index++)
        {
            Assert.True(intervals[index] < intervals[index - 1]);
        }
    }
}
