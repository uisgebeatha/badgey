using Badgey.Hardware;

namespace Badgey.Tests;

public class BadgeHidTransportTests
{
    [Fact]
    public void Constants_MatchConfirmedHidInterface()
    {
        Assert.Equal(0x0416, BadgeHidTransport.VendorId);
        Assert.Equal(0x5020, BadgeHidTransport.ProductId);
        Assert.Equal(0xFF00, BadgeHidTransport.UsagePage);
        Assert.Equal(1, BadgeHidTransport.Usage);
        Assert.Equal(64, BadgeHidTransport.PayloadReportLength);
        Assert.Equal(65, BadgeHidTransport.HostWriteBufferLength);
        Assert.Equal(8192, BadgeHidTransport.MaximumProtocolOutputLength);
    }

    [Fact]
    public void Write_RequiresAnOpenDevice()
    {
        using var transport = new BadgeHidTransport();

        var exception = Assert.Throws<InvalidOperationException>(() => transport.Write(new byte[64]));

        Assert.Contains("opened", exception.Message);
    }

    [Fact]
    public void Close_IsSafeWhenNotOpen()
    {
        using var transport = new BadgeHidTransport();

        transport.Close();

        Assert.False(transport.IsOpen);
    }

    [Fact]
    public void Dispose_IsIdempotentAndPreventsReuse()
    {
        var transport = new BadgeHidTransport();

        transport.Dispose();
        transport.Dispose();

        Assert.False(transport.IsOpen);
        Assert.Throws<ObjectDisposedException>(transport.Open);
        Assert.Throws<ObjectDisposedException>(() => transport.IsCompatibleBadgePresent());
    }
}
