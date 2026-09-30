using Badgey.Hardware;

namespace Badgey.Tests;

public class BadgeHidReportFramerTests
{
    [Fact]
    public void CreateReports_PrependsReportIdAndCopiesPayload()
    {
        var protocolOutput = Enumerable.Range(0, 64).Select(value => (byte)value).ToArray();

        var reports = BadgeHidReportFramer.CreateReports(protocolOutput);

        var report = Assert.Single(reports);
        Assert.Equal(65, report.Length);
        Assert.Equal(0, report[0]);
        Assert.Equal(protocolOutput, report[1..]);
    }

    [Fact]
    public void CreateReports_SplitsAlignedOutputIntoIndependentReports()
    {
        var protocolOutput = Enumerable.Range(0, 128).Select(value => (byte)value).ToArray();

        var reports = BadgeHidReportFramer.CreateReports(protocolOutput);

        Assert.Equal(2, reports.Count);
        Assert.Equal(0, reports[0][0]);
        Assert.Equal(protocolOutput[..64], reports[0][1..]);
        Assert.Equal(0, reports[1][0]);
        Assert.Equal(protocolOutput[64..], reports[1][1..]);
        Assert.NotSame(reports[0], reports[1]);
    }

    [Fact]
    public void CreateReports_RejectsNullOutput()
    {
        Assert.Throws<ArgumentNullException>(() => BadgeHidReportFramer.CreateReports(null!));
    }

    [Fact]
    public void CreateReports_RejectsEmptyOutput()
    {
        Assert.Throws<ArgumentException>(() => BadgeHidReportFramer.CreateReports([]));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(63)]
    [InlineData(65)]
    public void CreateReports_RejectsUnalignedOutput(int length)
    {
        Assert.Throws<ArgumentException>(() => BadgeHidReportFramer.CreateReports(new byte[length]));
    }

    [Fact]
    public void CreateReports_RejectsOutputLargerThanProtocolLimit()
    {
        Assert.Throws<ArgumentException>(() =>
            BadgeHidReportFramer.CreateReports(new byte[BadgeHidTransport.MaximumProtocolOutputLength + 64]));
    }
}
