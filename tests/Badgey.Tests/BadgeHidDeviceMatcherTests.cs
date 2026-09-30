using Badgey.Hardware;

namespace Badgey.Tests;

public class BadgeHidDeviceMatcherTests
{
    private const uint ConfirmedUsage = 0xFF000001;

    [Fact]
    public void IsCompatible_AcceptsConfirmedInterface()
    {
        var matches = BadgeHidDeviceMatcher.IsCompatible(
            vendorId: 0x0416,
            productId: 0x5020,
            manufacturer: "wch.cn",
            productName: "CH583",
            maximumOutputReportLength: 65,
            usages: [ConfirmedUsage]);

        Assert.True(matches);
    }

    [Theory]
    [InlineData(0x0417, 0x5020, "wch.cn", "CH583", 65, ConfirmedUsage)]
    [InlineData(0x0416, 0x5021, "wch.cn", "CH583", 65, ConfirmedUsage)]
    [InlineData(0x0416, 0x5020, "other", "CH583", 65, ConfirmedUsage)]
    [InlineData(0x0416, 0x5020, "wch.cn", "other", 65, ConfirmedUsage)]
    [InlineData(0x0416, 0x5020, "wch.cn", "CH583", 64, ConfirmedUsage)]
    [InlineData(0x0416, 0x5020, "wch.cn", "CH583", 65, 0xFF000002u)]
    public void IsCompatible_RejectsDifferentInterface(
        int vendorId,
        int productId,
        string manufacturer,
        string productName,
        int reportLength,
        uint usage)
    {
        var matches = BadgeHidDeviceMatcher.IsCompatible(
            vendorId,
            productId,
            manufacturer,
            productName,
            reportLength,
            [usage]);

        Assert.False(matches);
    }
}
