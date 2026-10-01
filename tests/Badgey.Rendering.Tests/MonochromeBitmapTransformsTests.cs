using Badgey.Rendering;

namespace Badgey.Rendering.Tests;

public class MonochromeBitmapTransformsTests
{
    [Fact]
    public void Invert_ReversesEveryPixelWithoutChangingSource()
    {
        var source = CreatePattern();

        var inverted = MonochromeBitmapTransforms.Invert(source);

        Assert.Equal(source.Width, inverted.Width);
        Assert.Equal(source.Height, inverted.Height);

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                Assert.NotEqual(source[x, y], inverted[x, y]);
            }
        }

        Assert.True(source[0, 0]);
        Assert.False(source[1, 0]);
        Assert.False(source[0, 1]);
        Assert.True(source[1, 1]);
    }

    [Fact]
    public void Invert_TwiceRestoresOriginalBitmap()
    {
        var source = CreatePattern();

        var restored = MonochromeBitmapTransforms.Invert(
            MonochromeBitmapTransforms.Invert(source));

        AssertBitmapsEqual(source, restored);
    }

    [Fact]
    public void Invert_RejectsNullSource()
    {
        Assert.Throws<ArgumentNullException>(
            () => MonochromeBitmapTransforms.Invert(null!));
    }

    [Fact]
    public void GraphicState_NewLoadResetsInversionAndRetainsNewSource()
    {
        var first = CreatePattern();
        var second = new MonochromeBitmap(width: 1, height: 2)
        {
            [0, 0] = true,
            [0, 1] = false,
        };
        var state = new GraphicBitmapState();

        state.Load(first);
        state.SetInverted(true);
        state.Load(second);

        Assert.False(state.IsInverted);
        Assert.Same(second, state.Source);
        Assert.Same(second, state.Current);
        Assert.True(first[0, 0]);
    }

    [Fact]
    public void GraphicState_RepeatedToggleAlwaysDerivesFromOriginalSource()
    {
        var source = CreatePattern();
        var state = new GraphicBitmapState();
        state.Load(source);

        state.SetInverted(true);
        var firstInversion = state.Current!;
        state.SetInverted(false);
        state.SetInverted(true);

        Assert.True(state.IsInverted);
        Assert.NotSame(firstInversion, state.Current);
        AssertBitmapsEqual(firstInversion, state.Current!);
        Assert.True(source[0, 0]);
        Assert.False(source[1, 0]);
    }

    [Fact]
    public void GraphicState_RejectsInversionBeforeLoad()
    {
        var state = new GraphicBitmapState();

        Assert.Throws<InvalidOperationException>(() => state.SetInverted(true));
    }

    [Fact]
    public void RepeatHorizontally_PreservesCopiesAndAddsFourColumnGaps()
    {
        var source = CreatePattern();

        var repeated = MonochromeBitmapRepeater.RepeatHorizontally(
            source,
            repetitions: 3,
            gapColumns: 4);

        Assert.Equal(14, repeated.Width);
        Assert.Equal(source.Height, repeated.Height);

        foreach (var left in new[] { 0, 6, 12 })
        {
            AssertCopyAt(source, repeated, left);
        }

        AssertColumnsUnlit(repeated, start: 2, count: 4);
        AssertColumnsUnlit(repeated, start: 8, count: 4);
        AssertBitmapsEqual(CreatePattern(), source);
    }

    [Fact]
    public void RepeatHorizontally_IsDeterministic()
    {
        var source = CreatePattern();

        var first = MonochromeBitmapRepeater.RepeatHorizontally(source, 3, 4);
        var second = MonochromeBitmapRepeater.RepeatHorizontally(source, 3, 4);

        AssertBitmapsEqual(first, second);
    }

    [Fact]
    public void RepeatHorizontally_UsesLitSeparatorsForInvertedGraphics()
    {
        var source = CreatePattern();

        var repeated = MonochromeBitmapRepeater.RepeatHorizontally(
            source,
            repetitions: 2,
            gapColumns: 4,
            gapValue: true);

        Assert.Equal(8, repeated.Width);
        AssertCopyAt(source, repeated, destinationLeft: 0);
        AssertCopyAt(source, repeated, destinationLeft: 6);
        AssertColumnsLit(repeated, start: 2, count: 4);
    }

    [Fact]
    public void RepeatToMaximumWidth_UsesMaximumCompleteCopyCountWithoutTruncating()
    {
        var source = new MonochromeBitmap(width: 3, height: 2)
        {
            [0, 0] = true,
            [2, 1] = true,
        };

        var repeated = MonochromeBitmapRepeater.RepeatToMaximumWidth(
            source,
            gapColumns: 4,
            maximumWidth: 20);

        Assert.Equal(17, repeated.Width);
        Assert.Equal(source.Height, repeated.Height);
        AssertCopyAt(source, repeated, destinationLeft: 0);
        AssertCopyAt(source, repeated, destinationLeft: 7);
        AssertCopyAt(source, repeated, destinationLeft: 14);
        AssertColumnsUnlit(repeated, start: 3, count: 4);
        AssertColumnsUnlit(repeated, start: 10, count: 4);
    }

    [Fact]
    public void RepeatHelpers_ValidateInputsAndCheckedWidth()
    {
        var source = new MonochromeBitmap(width: 1, height: 1);
        var empty = new MonochromeBitmap(width: 0, height: 1);

        Assert.Throws<ArgumentNullException>(
            () => MonochromeBitmapRepeater.RepeatHorizontally(null!, 1, 0));
        Assert.Throws<ArgumentException>(
            () => MonochromeBitmapRepeater.RepeatHorizontally(empty, 1, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonochromeBitmapRepeater.RepeatHorizontally(source, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonochromeBitmapRepeater.RepeatHorizontally(source, 1, -1));
        Assert.Throws<ArgumentNullException>(
            () => MonochromeBitmapRepeater.RepeatToMaximumWidth(null!, 0, 1));
        Assert.Throws<ArgumentException>(
            () => MonochromeBitmapRepeater.RepeatToMaximumWidth(empty, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonochromeBitmapRepeater.RepeatToMaximumWidth(source, -1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonochromeBitmapRepeater.RepeatToMaximumWidth(source, 0, 0));
        Assert.Throws<OverflowException>(
            () => MonochromeBitmapRepeater.RepeatHorizontally(
                source,
                int.MaxValue,
                int.MaxValue));
    }

    [Fact]
    public void Loop_MapsExactlyFourGapColumnsBeforeRepeating()
    {
        const int sourceWidth = 3;
        const int gapColumns = 4;

        Assert.Equal(7, MonochromeBitmapLoop.GetCycleWidth(sourceWidth, gapColumns));
        Assert.Equal(0, MonochromeBitmapLoop.GetSourceColumn(sourceWidth, gapColumns, 0));
        Assert.Equal(1, MonochromeBitmapLoop.GetSourceColumn(sourceWidth, gapColumns, 1));
        Assert.Equal(2, MonochromeBitmapLoop.GetSourceColumn(sourceWidth, gapColumns, 2));

        for (var x = 3; x < 7; x++)
        {
            Assert.Equal(-1, MonochromeBitmapLoop.GetSourceColumn(sourceWidth, gapColumns, x));
        }

        Assert.Equal(0, MonochromeBitmapLoop.GetSourceColumn(sourceWidth, gapColumns, 7));
        Assert.Equal(1, MonochromeBitmapLoop.GetSourceColumn(sourceWidth, gapColumns, 8));
    }

    [Fact]
    public void Loop_ValidatesInputsAndCheckedCycleWidth()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonochromeBitmapLoop.GetCycleWidth(0, 4));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonochromeBitmapLoop.GetCycleWidth(1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => MonochromeBitmapLoop.GetSourceColumn(1, 4, -1));
        Assert.Throws<OverflowException>(
            () => MonochromeBitmapLoop.GetCycleWidth(int.MaxValue, 1));
    }

    [Fact]
    public void GraphicState_RejectsNullSource()
    {
        var state = new GraphicBitmapState();

        Assert.Throws<ArgumentNullException>(() => state.Load(null!));
    }

    private static MonochromeBitmap CreatePattern() =>
        new(width: 2, height: 2)
        {
            [0, 0] = true,
            [1, 0] = false,
            [0, 1] = false,
            [1, 1] = true,
        };

    private static void AssertCopyAt(
        MonochromeBitmap source,
        MonochromeBitmap actual,
        int destinationLeft)
    {
        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                Assert.Equal(source[x, y], actual[destinationLeft + x, y]);
            }
        }
    }

    private static void AssertColumnsUnlit(
        MonochromeBitmap bitmap,
        int start,
        int count)
    {
        for (var x = start; x < start + count; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                Assert.False(bitmap[x, y]);
            }
        }
    }

    private static void AssertColumnsLit(
        MonochromeBitmap bitmap,
        int start,
        int count)
    {
        for (var x = start; x < start + count; x++)
        {
            for (var y = 0; y < bitmap.Height; y++)
            {
                Assert.True(bitmap[x, y]);
            }
        }
    }

    private static void AssertBitmapsEqual(
        MonochromeBitmap expected,
        MonochromeBitmap actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);

        for (var y = 0; y < expected.Height; y++)
        {
            for (var x = 0; x < expected.Width; x++)
            {
                Assert.Equal(expected[x, y], actual[x, y]);
            }
        }
    }
}
