using Badgey.Protocol;
using Badgey.Rendering;

namespace Badgey;

/// <summary>
/// Rendered content ready to be packed into one positional badge slot.
/// Exactly one of <see cref="Bitmap"/> and <see cref="Animation"/> must be set.
/// </summary>
internal sealed record BadgeSlotContent(
    MonochromeBitmap? Bitmap,
    MonochromeAnimation? Animation,
    BadgeDisplayMode Mode,
    int Speed,
    bool RepeatGraphic,
    bool RepeatGapIsLit)
{
    public static BadgeSlotContent FromBitmap(
        MonochromeBitmap bitmap,
        BadgeDisplayMode mode,
        int speed) =>
        new(bitmap, null, mode, speed, RepeatGraphic: false, RepeatGapIsLit: false);

    public static BadgeSlotContent FromGraphic(
        MonochromeBitmap bitmap,
        BadgeDisplayMode mode,
        int speed,
        bool isInverted) =>
        new(
            bitmap,
            null,
            mode,
            speed,
            RepeatGraphic: mode == BadgeDisplayMode.ScrollLeft,
            RepeatGapIsLit: isInverted);

    public static BadgeSlotContent FromAnimation(
        MonochromeAnimation animation,
        int speed) =>
        new(
            null,
            animation,
            BadgeDisplayMode.Animation,
            speed,
            RepeatGraphic: false,
            RepeatGapIsLit: false);
}

/// <summary>
/// Exact packet-aligned bytes prepared for one HID upload.
/// </summary>
internal sealed record PreparedBadgeUpload(byte[] Bytes, int PopulatedSlotCount)
{
    public int UploadSize => Bytes.Length;
}

/// <summary>
/// Converts ordered rendered slot content into one protocol upload without
/// touching hardware.
/// </summary>
internal static class BadgeMultiSlotUploadComposer
{
    internal const int SlotCount = BadgeProtocol.MaximumSlotCount;
    internal const int GraphicRepeatGapColumns = 4;
    internal const int MaximumPayloadByteColumns =
        (BadgeProtocol.MaximumUploadSize - BadgeProtocol.HeaderSize)
        / BadgeProtocol.DisplayHeight;

    public static PreparedBadgeUpload Prepare(
        IReadOnlyList<BadgeSlotContent?> slots) =>
        PrepareCore(slots, timestamp: null);

    public static PreparedBadgeUpload Prepare(
        IReadOnlyList<BadgeSlotContent?> slots,
        DateTime timestamp) =>
        PrepareCore(slots, timestamp);

    private static PreparedBadgeUpload PrepareCore(
        IReadOnlyList<BadgeSlotContent?> slots,
        DateTime? timestamp)
    {
        ArgumentNullException.ThrowIfNull(slots);

        if (slots.Count > SlotCount)
        {
            throw new ArgumentException(
                $"A badge upload supports at most {SlotCount} slots.",
                nameof(slots));
        }

        var prepared = new PreparedSlot?[SlotCount];
        var populatedSlotCount = 0;
        var naturalByteColumnCount = 0;
        var repeatingGraphicIndices = new List<int>();

        for (var slotIndex = 0; slotIndex < slots.Count; slotIndex++)
        {
            var content = slots[slotIndex];

            if (content is null)
            {
                continue;
            }

            var packedData = PackNaturalContent(content, slotIndex);
            var byteColumnCount = packedData.Length / BadgeProtocol.DisplayHeight;

            naturalByteColumnCount = checked(naturalByteColumnCount + byteColumnCount);
            populatedSlotCount++;
            prepared[slotIndex] = new PreparedSlot(content, packedData, byteColumnCount);

            if (content.RepeatGraphic
                && content.Bitmap is not null
                && content.Mode == BadgeDisplayMode.ScrollLeft)
            {
                repeatingGraphicIndices.Add(slotIndex);
            }
        }

        if (naturalByteColumnCount > MaximumPayloadByteColumns)
        {
            throw new ArgumentException(
                $"Combined slot content exceeds the {BadgeProtocol.MaximumUploadSize}-byte upload limit.",
                nameof(slots));
        }

        ExpandScrollingGraphics(
            prepared,
            repeatingGraphicIndices,
            MaximumPayloadByteColumns - naturalByteColumnCount);

        var protocolSlots = new BadgeSlotUpload?[SlotCount];

        for (var slotIndex = 0; slotIndex < prepared.Length; slotIndex++)
        {
            if (prepared[slotIndex] is not { } slot)
            {
                continue;
            }

            protocolSlots[slotIndex] = new BadgeSlotUpload(
                slot.PackedData,
                slot.Content.Mode,
                slot.Content.Speed);
        }

        var bytes = timestamp.HasValue
            ? BadgeProtocol.CreateMultiSlotUpload(protocolSlots, timestamp.Value)
            : BadgeProtocol.CreateMultiSlotUpload(protocolSlots);

        return new PreparedBadgeUpload(bytes, populatedSlotCount);
    }

    private static byte[] PackNaturalContent(
        BadgeSlotContent content,
        int slotIndex)
    {
        if ((content.Bitmap is null) == (content.Animation is null))
        {
            throw new ArgumentException(
                $"Slot {slotIndex + 1} must contain either one bitmap or one animation.",
                nameof(content));
        }

        if (content.Animation is { } animation)
        {
            if (content.Mode != BadgeDisplayMode.Animation)
            {
                throw new ArgumentException(
                    $"Slot {slotIndex + 1} animation content must use animation mode.",
                    nameof(content));
            }

            if (content.RepeatGraphic)
            {
                throw new ArgumentException(
                    $"Slot {slotIndex + 1} animation content cannot use graphic repetition.",
                    nameof(content));
            }

            return PackAnimation(animation);
        }

        if (content.Mode == BadgeDisplayMode.Animation)
        {
            throw new ArgumentException(
                $"Slot {slotIndex + 1} bitmap content cannot use animation mode.",
                nameof(content));
        }

        if (content.Bitmap!.Width == 0)
        {
            throw new ArgumentException(
                $"Slot {slotIndex + 1} bitmap must contain at least one column.",
                nameof(content));
        }

        return BadgeBitmapPacker.Pack(content.Bitmap);
    }

    private static byte[] PackAnimation(MonochromeAnimation animation)
    {
        var packed = new byte[checked(
            animation.FrameCount * BadgeProtocol.AnimationFrameDataSize)];
        var destinationOffset = 0;

        foreach (var frame in animation.Frames)
        {
            var packedFrame = BadgeBitmapPacker.Pack(frame);

            if (packedFrame.Length != BadgeProtocol.AnimationFrameDataSize)
            {
                throw new ArgumentException(
                    "Every animation frame must pack to exactly six byte-columns.",
                    nameof(animation));
            }

            packedFrame.CopyTo(packed, destinationOffset);
            destinationOffset += packedFrame.Length;
        }

        return packed;
    }

    private static void ExpandScrollingGraphics(
        PreparedSlot?[] prepared,
        IReadOnlyList<int> repeatingGraphicIndices,
        int remainingByteColumns)
    {
        if (repeatingGraphicIndices.Count == 0)
        {
            return;
        }

        var equalAdditionalColumns = remainingByteColumns / repeatingGraphicIndices.Count;
        var extraColumnCount = remainingByteColumns % repeatingGraphicIndices.Count;

        for (var repetitionIndex = 0;
             repetitionIndex < repeatingGraphicIndices.Count;
             repetitionIndex++)
        {
            var slotIndex = repeatingGraphicIndices[repetitionIndex];
            var slot = prepared[slotIndex]!;
            var additionalColumns = equalAdditionalColumns
                + (repetitionIndex < extraColumnCount ? 1 : 0);
            var maximumByteColumns = slot.NaturalByteColumnCount + additionalColumns;
            var maximumWidth = checked(maximumByteColumns * BadgeBitmapPacker.PixelsPerByte);
            var repeated = MonochromeBitmapRepeater.RepeatToMaximumWidth(
                slot.Content.Bitmap!,
                GraphicRepeatGapColumns,
                maximumWidth,
                slot.Content.RepeatGapIsLit);

            prepared[slotIndex] = slot with
            {
                PackedData = BadgeBitmapPacker.Pack(repeated),
            };
        }
    }

    private sealed record PreparedSlot(
        BadgeSlotContent Content,
        byte[] PackedData,
        int NaturalByteColumnCount);
}
