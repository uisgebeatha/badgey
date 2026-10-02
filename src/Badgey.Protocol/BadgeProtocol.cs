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
    public const int MaximumSlotCount = 8;
    public const int AnimationFrameStorageWidth = 48;
    public const int AnimationFrameByteColumns = 6;
    public const int AnimationFrameDataSize = AnimationFrameByteColumns * DisplayHeight;
    public const int MaximumAnimationFrameCount =
        (MaximumUploadSize - HeaderSize) / AnimationFrameDataSize;

    private const int SettingsOffset = 8;
    private const int MessageLengthOffset = 16;
    private const int TimestampOffset = 38;
    private const int SettingsSlotCount = MaximumSlotCount;

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

        return CreateUploadCore(bitmapData, mode, speed, timestamp);
    }

    /// <summary>
    /// Creates a packet-aligned animation upload from ordered, individually packed frames.
    /// Each logical 44 x 11 frame must be packed separately into the protocol's six
    /// byte-columns (48 stored pixels), so frame boundaries remain byte-aligned.
    /// </summary>
    public static byte[] CreateAnimationUpload(
        IReadOnlyList<byte[]> packedFrames,
        int speed)
    {
        return CreateAnimationUpload(packedFrames, speed, DateTime.Now);
    }

    /// <summary>
    /// Creates a packet-aligned animation upload using an explicit timestamp.
    /// This overload is useful when reproducible output is required.
    /// </summary>
    public static byte[] CreateAnimationUpload(
        IReadOnlyList<byte[]> packedFrames,
        int speed,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(packedFrames);
        ValidateSpeed(speed);

        if (packedFrames.Count == 0)
        {
            throw new ArgumentException(
                "Animation must contain at least one frame.",
                nameof(packedFrames));
        }

        if (packedFrames.Count > MaximumAnimationFrameCount)
        {
            throw new ArgumentException(
                $"Animation must not contain more than {MaximumAnimationFrameCount} frames " +
                $"within the {MaximumUploadSize}-byte upload limit.",
                nameof(packedFrames));
        }

        var bitmapData = new byte[packedFrames.Count * AnimationFrameDataSize];

        for (var frameIndex = 0; frameIndex < packedFrames.Count; frameIndex++)
        {
            var frame = packedFrames[frameIndex];

            if (frame is null)
            {
                throw new ArgumentException(
                    $"Animation frame {frameIndex} must not be null.",
                    nameof(packedFrames));
            }

            if (frame.Length != AnimationFrameDataSize)
            {
                throw new ArgumentException(
                    $"Animation frame {frameIndex} must contain exactly " +
                    $"{AnimationFrameDataSize} packed bytes ({AnimationFrameByteColumns} byte-columns).",
                    nameof(packedFrames));
            }

            frame.CopyTo(bitmapData, frameIndex * AnimationFrameDataSize);
        }

        return CreateUploadCore(bitmapData, BadgeDisplayMode.Animation, speed, timestamp);
    }

    /// <summary>
    /// Creates a packet-aligned upload for up to eight positional badge slots.
    /// A null entry represents an empty slot and preserves its position.
    /// </summary>
    public static byte[] CreateMultiSlotUpload(
        IReadOnlyList<BadgeSlotUpload?> slots)
    {
        return CreateMultiSlotUpload(slots, DateTime.Now);
    }

    /// <summary>
    /// Creates a packet-aligned upload for up to eight positional badge slots
    /// using an explicit timestamp.
    /// </summary>
    public static byte[] CreateMultiSlotUpload(
        IReadOnlyList<BadgeSlotUpload?> slots,
        DateTime timestamp)
    {
        ArgumentNullException.ThrowIfNull(slots);

        if (slots.Count is < 1 or > MaximumSlotCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slots),
                slots.Count,
                $"The upload must describe from 1 to {MaximumSlotCount} positional slots.");
        }

        long totalPayloadLength = 0;
        var populatedSlotCount = 0;

        for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            var slot = slots[slotIndex];
            if (slot is null)
            {
                continue;
            }

            ValidateSlot(slot, slotIndex);
            totalPayloadLength += slot.PackedData.Length;
            populatedSlotCount++;
        }

        if (populatedSlotCount == 0)
        {
            throw new ArgumentException(
                "At least one badge slot must contain content.",
                nameof(slots));
        }

        var unpaddedLength = HeaderSize + totalPayloadLength;
        var uploadLength = ((unpaddedLength + PacketSize - 1) / PacketSize) * PacketSize;

        if (uploadLength > MaximumUploadSize)
        {
            throw new ArgumentException(
                $"The combined packet-aligned upload must not exceed {MaximumUploadSize} bytes.",
                nameof(slots));
        }

        var upload = new byte[(int)uploadLength];
        HeaderTemplate.CopyTo(upload, 0);

        var payloadOffset = HeaderSize;

        for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            var slot = slots[slotIndex];
            if (slot is null)
            {
                continue;
            }

            upload[SettingsOffset + slotIndex] =
                (byte)(((slot.Speed - 1) << 4) | (byte)slot.Mode);

            var lengthInByteColumns = slot.PackedData.Length / DisplayHeight;
            var lengthOffset = MessageLengthOffset + (slotIndex * 2);
            upload[lengthOffset] = (byte)(lengthInByteColumns >> 8);
            upload[lengthOffset + 1] = (byte)lengthInByteColumns;

            slot.PackedData.CopyTo(upload, payloadOffset);
            payloadOffset += slot.PackedData.Length;
        }

        upload[TimestampOffset] = (byte)(timestamp.Year % 100);
        upload[TimestampOffset + 1] = (byte)timestamp.Month;
        upload[TimestampOffset + 2] = (byte)timestamp.Day;
        upload[TimestampOffset + 3] = (byte)timestamp.Hour;
        upload[TimestampOffset + 4] = (byte)timestamp.Minute;
        upload[TimestampOffset + 5] = (byte)timestamp.Second;

        return upload;
    }

    private static byte[] CreateUploadCore(
        ReadOnlySpan<byte> bitmapData,
        BadgeDisplayMode mode,
        int speed,
        DateTime timestamp)
    {
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

        ValidateSpeed(speed);

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

    private static void ValidateSpeed(int speed)
    {
        if (speed is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(nameof(speed), speed, "Speed must be from 1 to 8.");
        }
    }

    private static void ValidateSlot(BadgeSlotUpload slot, int slotIndex)
    {
        if (slot.Mode is not BadgeDisplayMode.ScrollLeft
            and not BadgeDisplayMode.StaticCentered
            and not BadgeDisplayMode.Animation)
        {
            throw new ArgumentOutOfRangeException(
                nameof(slot.Mode),
                slot.Mode,
                $"Slot {slotIndex + 1} uses an unsupported display mode.");
        }

        ValidateSpeed(slot.Speed);

        if (slot.PackedData is null)
        {
            throw new ArgumentException(
                $"Slot {slotIndex + 1} packed data must not be null.",
                "slots");
        }

        if (slot.PackedData.Length == 0)
        {
            throw new ArgumentException(
                $"Slot {slotIndex + 1} packed data must not be empty.",
                "slots");
        }

        var requiredMultiple = slot.Mode == BadgeDisplayMode.Animation
            ? AnimationFrameDataSize
            : DisplayHeight;

        if (slot.PackedData.Length % requiredMultiple != 0)
        {
            var description = slot.Mode == BadgeDisplayMode.Animation
                ? $"complete {AnimationFrameDataSize}-byte animation frames"
                : $"exactly {DisplayHeight} bytes per byte-column";

            throw new ArgumentException(
                $"Slot {slotIndex + 1} packed data must contain {description}.",
                "slots");
        }
    }

    private static int RoundUpToPacketSize(int length) =>
        checked(((length + PacketSize - 1) / PacketSize) * PacketSize);
}
