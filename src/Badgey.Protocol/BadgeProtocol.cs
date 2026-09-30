namespace Badgey.Protocol;

/// <summary>
/// Builds legacy protocol uploads for the confirmed 44 x 11 LSLED badge.
/// </summary>
public static class BadgeProtocol
{
    public const int DisplayWidth = 44;
    public const int DisplayHeight = 11;
    public const int HeaderSize = 64;
    public const int PacketSize = 64;
    public const int MaximumUploadSize = 8192;

    private const int SettingsOffset = 8;
    private const int MessageLengthOffset = 16;
    private const int TimestampOffset = 38;
    private const int SettingsSlotCount = 8;

    private static readonly byte[] HeaderTemplate =
    [
        0x77, 0x61, 0x6E, 0x67, 0x00, 0x00, 0x00, 0x00,
        0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40, 0x40,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
    ];

    /// <summary>
    /// Creates a packet-aligned upload from bitmap data packed as 11 bytes per
    /// eight-pixel-wide column, ordered one column after another.
    /// </summary>
    public static byte[] CreateUpload(
        byte[] bitmapData,
        BadgeDisplayMode mode,
        int speed)
    {
        ArgumentNullException.ThrowIfNull(bitmapData);
        return CreateUpload(bitmapData.AsSpan(), mode, speed, DateTime.Now);
    }

    /// <summary>
    /// Creates a packet-aligned upload using an explicit timestamp.
    /// This overload is useful when reproducible output is required.
    /// </summary>
    public static byte[] CreateUpload(
        byte[] bitmapData,
        BadgeDisplayMode mode,
        int speed,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(bitmapData);
        return CreateUpload(bitmapData.AsSpan(), mode, speed, timestamp);
    }

    /// <summary>
    /// Creates a packet-aligned upload from bitmap data packed as 11 bytes per
    /// eight-pixel-wide column, ordered one column after another.
    /// </summary>
    public static byte[] CreateUpload(
        ReadOnlySpan<byte> bitmapData,
        BadgeDisplayMode mode,
        int speed,
        DateTime timestamp)
    {
        ValidateInput(bitmapData.Length, mode, speed);

        var messageLengthInByteColumns = bitmapData.Length / DisplayHeight;
        var unpaddedLength = HeaderSize + bitmapData.Length;
        var uploadLength = RoundUpToPacketSize(unpaddedLength);
        var upload = new byte[uploadLength];

        HeaderTemplate.CopyTo(upload, 0);

        var encodedSettings = (byte)(((speed - 1) << 4) | (byte)mode);
        upload.AsSpan(SettingsOffset, SettingsSlotCount).Fill(encodedSettings);

        upload[MessageLengthOffset] = (byte)(messageLengthInByteColumns >> 8);
        upload[MessageLengthOffset + 1] = (byte)messageLengthInByteColumns;

        upload[TimestampOffset] = (byte)(timestamp.Year % 100);
        upload[TimestampOffset + 1] = (byte)timestamp.Month;
        upload[TimestampOffset + 2] = (byte)timestamp.Day;
        upload[TimestampOffset + 3] = (byte)timestamp.Hour;
        upload[TimestampOffset + 4] = (byte)timestamp.Minute;
        upload[TimestampOffset + 5] = (byte)timestamp.Second;

        bitmapData.CopyTo(upload.AsSpan(HeaderSize));
        return upload;
    }

    private static void ValidateInput(int bitmapLength, BadgeDisplayMode mode, int speed)
    {
        if (mode is not BadgeDisplayMode.ScrollLeft and not BadgeDisplayMode.StaticCentered)
        {
            throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "Only scroll-left and static-centred modes are supported.");
        }

        if (speed is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed must be from 1 to 8.");
        }

        if (bitmapLength == 0)
        {
            throw new ArgumentException("Bitmap data must not be empty.", "bitmapData");
        }

        if (bitmapLength % DisplayHeight != 0)
        {
            throw new ArgumentException(
                $"Bitmap data must contain exactly {DisplayHeight} bytes per byte-column.",
                "bitmapData");
        }

        if (RoundUpToPacketSize(HeaderSize + bitmapLength) > MaximumUploadSize)
        {
            throw new ArgumentException(
                $"The packet-aligned upload must not exceed {MaximumUploadSize} bytes.",
                "bitmapData");
        }
    }

    private static int RoundUpToPacketSize(int length) =>
        checked(((length + PacketSize - 1) / PacketSize) * PacketSize);
}
