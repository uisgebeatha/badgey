using Badgey.Protocol;

namespace Badgey.Tests;

public class BadgeProtocolTests
{
    private static readonly DateTime TestTimestamp = new(2026, 9, 30, 12, 34, 56);

    [Fact]
    public void Dimensions_MatchConfirmedBadge()
    {
        Assert.Equal(44, BadgeProtocol.DisplayWidth);
        Assert.Equal(11, BadgeProtocol.DisplayHeight);
    }

    [Fact]
    public void CreateUpload_ConstructsKnownHeader()
    {
        var upload = BadgeProtocol.CreateUpload(
            new byte[22],
            BadgeDisplayMode.StaticCentered,
            speed: 4,
            TestTimestamp);

        byte[] expectedHeader =
        [
            0x77, 0x61, 0x6E, 0x67, 0x00, 0x00, 0x00, 0x00,
            0x34, 0x34, 0x34, 0x34, 0x34, 0x34, 0x34, 0x34,
            0x00, 0x02, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            26, 9, 30, 12, 34, 56,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00,
        ];

        Assert.Equal(expectedHeader, upload[..BadgeProtocol.HeaderSize]);
    }

    [Theory]
    [InlineData(BadgeDisplayMode.ScrollLeft, 0x00)]
    [InlineData(BadgeDisplayMode.StaticCentered, 0x04)]
    public void CreateUpload_EncodesModeInAllSettingsSlots(BadgeDisplayMode mode, byte expected)
    {
        var upload = BadgeProtocol.CreateUpload(new byte[11], mode, speed: 1, TestTimestamp);

        Assert.All(upload[8..16], value => Assert.Equal(expected, value));
    }

    [Theory]
    [InlineData(1, 0x00)]
    [InlineData(2, 0x10)]
    [InlineData(3, 0x20)]
    [InlineData(4, 0x30)]
    [InlineData(5, 0x40)]
    [InlineData(6, 0x50)]
    [InlineData(7, 0x60)]
    [InlineData(8, 0x70)]
    public void CreateUpload_EncodesSpeed(int speed, byte expected)
    {
        var upload = BadgeProtocol.CreateUpload(
            new byte[11],
            BadgeDisplayMode.ScrollLeft,
            speed,
            TestTimestamp);

        Assert.Equal(expected, upload[8]);
    }

    [Fact]
    public void CreateUpload_EncodesMessageLengthAsBigEndianByteColumns()
    {
        var upload = BadgeProtocol.CreateUpload(
            new byte[256 * BadgeProtocol.DisplayHeight],
            BadgeDisplayMode.ScrollLeft,
            speed: 1,
            TestTimestamp);

        Assert.Equal(0x01, upload[16]);
        Assert.Equal(0x00, upload[17]);
    }

    [Fact]
    public void CreateUpload_PreservesBitmapAndPadsToPacketBoundary()
    {
        var bitmap = Enumerable.Range(1, BadgeProtocol.DisplayHeight).Select(value => (byte)value).ToArray();

        var upload = BadgeProtocol.CreateUpload(
            bitmap,
            BadgeDisplayMode.ScrollLeft,
            speed: 1,
            TestTimestamp);

        Assert.Equal(128, upload.Length);
        Assert.Equal(0, upload.Length % BadgeProtocol.PacketSize);
        Assert.Equal(bitmap, upload[BadgeProtocol.HeaderSize..(BadgeProtocol.HeaderSize + bitmap.Length)]);
        Assert.All(upload[(BadgeProtocol.HeaderSize + bitmap.Length)..], value => Assert.Equal(0, value));
    }

    [Fact]
    public void CreateUpload_DoesNotAddAnExtraPacketWhenAlreadyAligned()
    {
        var upload = BadgeProtocol.CreateUpload(
            new byte[64 * BadgeProtocol.DisplayHeight],
            BadgeDisplayMode.ScrollLeft,
            speed: 1,
            TestTimestamp);

        Assert.Equal(768, upload.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void CreateUpload_RejectsInvalidSpeed(int speed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BadgeProtocol.CreateUpload(new byte[11], BadgeDisplayMode.ScrollLeft, speed, TestTimestamp));
    }

    [Fact]
    public void CreateUpload_RejectsUnsupportedMode()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BadgeProtocol.CreateUpload(new byte[11], (BadgeDisplayMode)1, speed: 1, TestTimestamp));
    }

    [Fact]
    public void CreateUpload_RejectsNullBitmap()
    {
        Assert.Throws<ArgumentNullException>(() =>
            BadgeProtocol.CreateUpload(null!, BadgeDisplayMode.ScrollLeft, speed: 1, TestTimestamp));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    public void CreateUpload_RejectsInvalidBitmapLength(int length)
    {
        Assert.Throws<ArgumentException>(() =>
            BadgeProtocol.CreateUpload(new byte[length], BadgeDisplayMode.ScrollLeft, speed: 1, TestTimestamp));
    }

    [Fact]
    public void CreateUpload_RejectsUploadLargerThanProtocolLimit()
    {
        var bitmap = new byte[739 * BadgeProtocol.DisplayHeight];

        Assert.Throws<ArgumentException>(() =>
            BadgeProtocol.CreateUpload(bitmap, BadgeDisplayMode.ScrollLeft, speed: 1, TestTimestamp));
    }
}
