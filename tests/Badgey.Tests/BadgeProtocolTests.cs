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

    [Fact]
    public void CreateAnimationUpload_EncodesModeSpeedAndTotalByteColumnLength()
    {
        byte[][] frames =
        [
            new byte[BadgeProtocol.AnimationFrameDataSize],
            new byte[BadgeProtocol.AnimationFrameDataSize],
        ];

        var upload = BadgeProtocol.CreateAnimationUpload(frames, speed: 8, TestTimestamp);

        Assert.All(upload[8..16], value => Assert.Equal(0x75, value));
        Assert.Equal(0x00, upload[16]);
        Assert.Equal(0x0C, upload[17]);
    }

    [Fact]
    public void CreateAnimationUpload_PreservesFrameOrderAndPacketAlignment()
    {
        var firstFrame = Enumerable.Range(0, BadgeProtocol.AnimationFrameDataSize)
            .Select(value => (byte)value)
            .ToArray();
        var secondFrame = Enumerable.Range(0, BadgeProtocol.AnimationFrameDataSize)
            .Select(value => (byte)(255 - value))
            .ToArray();

        var upload = BadgeProtocol.CreateAnimationUpload(
            [firstFrame, secondFrame],
            speed: 4,
            TestTimestamp);

        Assert.Equal(0, upload.Length % BadgeProtocol.PacketSize);
        Assert.Equal(
            firstFrame,
            upload[BadgeProtocol.HeaderSize..(BadgeProtocol.HeaderSize + firstFrame.Length)]);
        Assert.Equal(
            secondFrame,
            upload[(BadgeProtocol.HeaderSize + firstFrame.Length)..
                (BadgeProtocol.HeaderSize + firstFrame.Length + secondFrame.Length)]);
        Assert.All(
            upload[(BadgeProtocol.HeaderSize + firstFrame.Length + secondFrame.Length)..],
            value => Assert.Equal(0, value));
    }

    [Fact]
    public void CreateAnimationUpload_AcceptsMaximumSafeFrameCount()
    {
        var frames = Enumerable.Range(0, BadgeProtocol.MaximumAnimationFrameCount)
            .Select(_ => new byte[BadgeProtocol.AnimationFrameDataSize])
            .ToArray();

        var upload = BadgeProtocol.CreateAnimationUpload(frames, speed: 1, TestTimestamp);

        Assert.Equal(123, BadgeProtocol.MaximumAnimationFrameCount);
        Assert.Equal(BadgeProtocol.MaximumUploadSize, upload.Length);
        Assert.Equal(0x02, upload[16]);
        Assert.Equal(0xE2, upload[17]);
    }

    [Fact]
    public void CreateAnimationUpload_RejectsFrameCountOverUploadLimitWithoutTruncating()
    {
        var frames = Enumerable.Range(0, BadgeProtocol.MaximumAnimationFrameCount + 1)
            .Select(_ => new byte[BadgeProtocol.AnimationFrameDataSize])
            .ToArray();

        var exception = Assert.Throws<ArgumentException>(() =>
            BadgeProtocol.CreateAnimationUpload(frames, speed: 1, TestTimestamp));

        Assert.Contains(BadgeProtocol.MaximumAnimationFrameCount.ToString(), exception.Message);
    }

    [Fact]
    public void CreateAnimationUpload_RejectsNullFrameCollection()
    {
        Assert.Throws<ArgumentNullException>(() =>
            BadgeProtocol.CreateAnimationUpload(null!, speed: 1, TestTimestamp));
    }

    [Fact]
    public void CreateAnimationUpload_RejectsEmptyFrameCollection()
    {
        Assert.Throws<ArgumentException>(() =>
            BadgeProtocol.CreateAnimationUpload([], speed: 1, TestTimestamp));
    }

    [Fact]
    public void CreateAnimationUpload_RejectsNullFrame()
    {
        Assert.Throws<ArgumentException>(() =>
            BadgeProtocol.CreateAnimationUpload([null!], speed: 1, TestTimestamp));
    }

    [Theory]
    [InlineData(65)]
    [InlineData(67)]
    public void CreateAnimationUpload_RejectsIncorrectPackedFrameSize(int frameSize)
    {
        Assert.Throws<ArgumentException>(() =>
            BadgeProtocol.CreateAnimationUpload([new byte[frameSize]], speed: 1, TestTimestamp));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public void CreateAnimationUpload_RejectsInvalidSpeed(int speed)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BadgeProtocol.CreateAnimationUpload(
                [new byte[BadgeProtocol.AnimationFrameDataSize]],
                speed,
                TestTimestamp));
    }

    [Fact]
    public void CreateUpload_StillRejectsAnimationModeWithoutFrameValidation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            BadgeProtocol.CreateUpload(
                new byte[BadgeProtocol.AnimationFrameDataSize],
                BadgeDisplayMode.Animation,
                speed: 1,
                TestTimestamp));
    }
}
