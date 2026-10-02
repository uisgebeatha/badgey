using Badgey.Protocol;
using Badgey.Rendering;

namespace Badgey.Wpf.Tests;

public class BadgeSlotStateAndComposerTests
{
    private static readonly DateTime TestTimestamp = new(2026, 10, 2, 12, 34, 56);

    [Fact]
    public void SlotState_PopulationFollowsActiveContentKind()
    {
        var state = new BadgeSlotState(slotNumber: 3);
        var bitmap = CreateBitmap(width: 8, (0, 0));
        var animation = new MonochromeAnimation(
            [CreateBitmap(MonochromeAnimation.FrameWidth, (1, 1))]);

        state.SetTextRenderResult("Hello", bitmap, isValid: true);
        Assert.True(state.IsPopulated);
        Assert.Equal("3 •", state.SelectorLabel);

        state.ContentKind = BadgeContentKind.Graphic;
        Assert.False(state.IsPopulated);

        state.LoadGraphic(bitmap, "graphic.png");
        Assert.True(state.IsPopulated);

        state.ContentKind = BadgeContentKind.Animation;
        Assert.False(state.IsPopulated);

        state.LoadAnimation(animation, "animation.gif");
        Assert.True(state.IsPopulated);

        state.ContentKind = BadgeContentKind.Text;
        Assert.True(state.IsPopulated);
        Assert.Equal("Hello", state.Message);
    }

    [Fact]
    public void SlotState_RefreshPopulationNotifiesSelectorProperties()
    {
        var state = new BadgeSlotState(slotNumber: 1);
        var changedProperties = new List<string?>();
        state.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName);

        state.SetTextRenderResult(
            "A",
            CreateBitmap(width: 1, (0, 0)),
            isValid: true);

        Assert.Contains(nameof(BadgeSlotState.IsPopulated), changedProperties);
        Assert.Contains(nameof(BadgeSlotState.SelectorLabel), changedProperties);
    }

    [Fact]
    public void SlotState_ClearResetsEveryDraftAndSetting()
    {
        var state = new BadgeSlotState(slotNumber: 5);
        var graphic = CreateBitmap(width: 8, (0, 0));
        var animation = new MonochromeAnimation(
            [CreateBitmap(MonochromeAnimation.FrameWidth, (2, 2))]);

        state.SetTextRenderResult("Text", graphic, isValid: true);
        state.LoadGraphic(graphic, "graphic.png");
        state.SetGraphicInverted(true);
        state.LoadAnimation(animation, "animation.gif");
        state.SetAnimationInverted(true);
        state.ContentKind = BadgeContentKind.Animation;
        state.Mode = BadgeDisplayMode.StaticCentered;
        state.Speed = 8;

        state.Clear();

        Assert.Equal(5, state.SlotNumber);
        Assert.Equal(BadgeContentKind.Text, state.ContentKind);
        Assert.Equal(string.Empty, state.Message);
        Assert.Equal(0, state.TextBitmap.Width);
        Assert.False(state.TextPreviewIsValid);
        Assert.Null(state.GraphicState.Source);
        Assert.Null(state.AnimationState.Source);
        Assert.Equal(BadgeSlotState.NoGraphicFileName, state.GraphicFileName);
        Assert.Equal(BadgeSlotState.NoAnimationFileName, state.AnimationFileName);
        Assert.Equal(BadgeDisplayMode.ScrollLeft, state.Mode);
        Assert.Equal(4, state.Speed);
        Assert.False(state.IsPopulated);
        Assert.Equal("5", state.SelectorLabel);
    }

    [Fact]
    public void Composer_OneBitmapSlotMatchesProtocolInputExactly()
    {
        var bitmap = CreateBitmap(width: 8, (0, 0), (7, 10));
        BadgeSlotContent?[] content =
        [
            BadgeSlotContent.FromBitmap(
                bitmap,
                BadgeDisplayMode.StaticCentered,
                speed: 3),
        ];

        var prepared = BadgeMultiSlotUploadComposer.Prepare(content, TestTimestamp);
        var packed = BadgeBitmapPacker.Pack(bitmap);
        var expected = BadgeProtocol.CreateMultiSlotUpload(
            [new BadgeSlotUpload(packed, BadgeDisplayMode.StaticCentered, Speed: 3)],
            TestTimestamp);

        Assert.Equal(1, prepared.PopulatedSlotCount);
        Assert.Equal(expected.Length, prepared.UploadSize);
        Assert.Equal(expected, prepared.Bytes);
    }

    [Fact]
    public void Composer_MixedSlotsPreserveEmptyPositionsAndPayloadOrder()
    {
        var text = CreateBitmap(width: 8, (0, 0));
        var graphic = CreateBitmap(width: 8, (1, 1));
        var firstFrame = CreateBitmap(MonochromeAnimation.FrameWidth, (2, 2));
        var secondFrame = CreateBitmap(MonochromeAnimation.FrameWidth, (3, 3));
        var animation = new MonochromeAnimation([firstFrame, secondFrame]);
        BadgeSlotContent?[] content =
        [
            BadgeSlotContent.FromBitmap(text, BadgeDisplayMode.ScrollLeft, speed: 2),
            null,
            BadgeSlotContent.FromGraphic(
                graphic,
                BadgeDisplayMode.StaticCentered,
                speed: 5,
                isInverted: false),
            null,
            BadgeSlotContent.FromAnimation(animation, speed: 7),
        ];

        var prepared = BadgeMultiSlotUploadComposer.Prepare(content, TestTimestamp);
        var expectedAnimation = BadgeBitmapPacker.Pack(firstFrame)
            .Concat(BadgeBitmapPacker.Pack(secondFrame))
            .ToArray();
        var expected = BadgeProtocol.CreateMultiSlotUpload(
            [
                new BadgeSlotUpload(
                    BadgeBitmapPacker.Pack(text),
                    BadgeDisplayMode.ScrollLeft,
                    Speed: 2),
                null,
                new BadgeSlotUpload(
                    BadgeBitmapPacker.Pack(graphic),
                    BadgeDisplayMode.StaticCentered,
                    Speed: 5),
                null,
                new BadgeSlotUpload(
                    expectedAnimation,
                    BadgeDisplayMode.Animation,
                    Speed: 7),
            ],
            TestTimestamp);

        Assert.Equal(3, prepared.PopulatedSlotCount);
        Assert.Equal(expected, prepared.Bytes);

        var payload = prepared.Bytes.AsSpan(BadgeProtocol.HeaderSize);
        Assert.True(payload[..11].SequenceEqual(BadgeBitmapPacker.Pack(text)));
        Assert.True(payload.Slice(11, 11).SequenceEqual(BadgeBitmapPacker.Pack(graphic)));
        Assert.True(payload.Slice(22, expectedAnimation.Length).SequenceEqual(expectedAnimation));
    }

    [Fact]
    public void Composer_AnimationPacksEveryFrameSeparatelyInFrameOrder()
    {
        var first = CreateBitmap(MonochromeAnimation.FrameWidth, (0, 0), (43, 10));
        var second = CreateBitmap(MonochromeAnimation.FrameWidth, (8, 1), (40, 9));
        var animation = new MonochromeAnimation([first, second]);

        var prepared = BadgeMultiSlotUploadComposer.Prepare(
            [BadgeSlotContent.FromAnimation(animation, speed: 4)],
            TestTimestamp);

        var expectedFirst = BadgeBitmapPacker.Pack(first);
        var expectedSecond = BadgeBitmapPacker.Pack(second);
        var payload = prepared.Bytes.AsSpan(BadgeProtocol.HeaderSize);

        Assert.True(payload[..expectedFirst.Length].SequenceEqual(expectedFirst));
        Assert.True(payload.Slice(expectedFirst.Length, expectedSecond.Length)
            .SequenceEqual(expectedSecond));
    }

    [Fact]
    public void Composer_SingleScrollingGraphicRetainsFullCapacityTiling()
    {
        var source = CreateBitmap(width: 8, (0, 0), (7, 10));
        var content = BadgeSlotContent.FromGraphic(
            source,
            BadgeDisplayMode.ScrollLeft,
            speed: 4,
            isInverted: false);

        var prepared = BadgeMultiSlotUploadComposer.Prepare([content], TestTimestamp);
        var expectedRepeated = MonochromeBitmapRepeater.RepeatToMaximumWidth(
            source,
            BadgeMultiSlotUploadComposer.GraphicRepeatGapColumns,
            BadgeMultiSlotUploadComposer.MaximumPayloadByteColumns
                * BadgeBitmapPacker.PixelsPerByte);
        var expectedPacked = BadgeBitmapPacker.Pack(expectedRepeated);

        Assert.Equal(BadgeProtocol.MaximumUploadSize, prepared.UploadSize);
        Assert.True(prepared.Bytes.AsSpan(BadgeProtocol.HeaderSize, expectedPacked.Length)
            .SequenceEqual(expectedPacked));
    }

    [Fact]
    public void Composer_MultipleScrollingGraphicsShareRemainingCapacityEvenly()
    {
        var first = CreateBitmap(width: 8, (0, 0));
        var second = CreateBitmap(width: 8, (7, 10));
        BadgeSlotContent?[] content =
        [
            BadgeSlotContent.FromGraphic(
                first,
                BadgeDisplayMode.ScrollLeft,
                speed: 2,
                isInverted: false),
            BadgeSlotContent.FromGraphic(
                second,
                BadgeDisplayMode.ScrollLeft,
                speed: 6,
                isInverted: true),
        ];

        var prepared = BadgeMultiSlotUploadComposer.Prepare(content, TestTimestamp);

        const int expectedColumnsPerGraphic =
            BadgeMultiSlotUploadComposer.MaximumPayloadByteColumns / 2;
        var expectedFirst = MonochromeBitmapRepeater.RepeatToMaximumWidth(
            first,
            BadgeMultiSlotUploadComposer.GraphicRepeatGapColumns,
            expectedColumnsPerGraphic * BadgeBitmapPacker.PixelsPerByte,
            gapValue: false);
        var expectedSecond = MonochromeBitmapRepeater.RepeatToMaximumWidth(
            second,
            BadgeMultiSlotUploadComposer.GraphicRepeatGapColumns,
            expectedColumnsPerGraphic * BadgeBitmapPacker.PixelsPerByte,
            gapValue: true);
        var packedFirst = BadgeBitmapPacker.Pack(expectedFirst);
        var packedSecond = BadgeBitmapPacker.Pack(expectedSecond);
        var payload = prepared.Bytes.AsSpan(BadgeProtocol.HeaderSize);

        Assert.Equal(BadgeProtocol.MaximumUploadSize, prepared.UploadSize);
        Assert.True(payload[..packedFirst.Length].SequenceEqual(packedFirst));
        Assert.True(payload.Slice(packedFirst.Length, packedSecond.Length)
            .SequenceEqual(packedSecond));
    }

    [Fact]
    public void Composer_RejectsOversizedNaturalCombinedContentWithoutTruncation()
    {
        var first = CreateBitmap(width: 3_000, (0, 0));
        var second = CreateBitmap(width: 3_000, (2_999, 10));

        var exception = Assert.Throws<ArgumentException>(() =>
            BadgeMultiSlotUploadComposer.Prepare(
                [
                    BadgeSlotContent.FromBitmap(
                        first,
                        BadgeDisplayMode.ScrollLeft,
                        speed: 1),
                    BadgeSlotContent.FromBitmap(
                        second,
                        BadgeDisplayMode.ScrollLeft,
                        speed: 1),
                ],
                TestTimestamp));

        Assert.Contains(BadgeProtocol.MaximumUploadSize.ToString(), exception.Message);
    }

    private static MonochromeBitmap CreateBitmap(
        int width,
        params (int X, int Y)[] litPixels)
    {
        var bitmap = new MonochromeBitmap(width, BadgeBitmapPacker.RequiredHeight);

        foreach (var (x, y) in litPixels)
        {
            bitmap[x, y] = true;
        }

        return bitmap;
    }
}
