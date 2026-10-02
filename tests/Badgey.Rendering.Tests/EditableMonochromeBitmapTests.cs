using Badgey.Rendering;

namespace Badgey.Rendering.Tests;

public class EditableMonochromeBitmapTests
{
    [Fact]
    public void SetClearAndToggle_ChangeOnlyTheRequestedPixel()
    {
        var editor = new EditableMonochromeBitmap();

        editor.SetPixel(3, 4);
        Assert.True(editor[3, 4]);
        Assert.False(editor[4, 4]);

        editor.TogglePixel(3, 4);
        Assert.False(editor[3, 4]);

        editor.TogglePixel(3, 4);
        editor.ClearPixel(3, 4);
        Assert.False(editor[3, 4]);
    }

    [Fact]
    public void ClearFillAndInvert_AffectTheCompleteCanvas()
    {
        var editor = new EditableMonochromeBitmap();

        editor.FillAll();
        Assert.Equal(
            EditableMonochromeBitmap.CanvasWidth
                * EditableMonochromeBitmap.CanvasHeight,
            CountLitPixels(editor.CreateBitmap()));

        editor.Invert();
        Assert.Equal(0, CountLitPixels(editor.CreateBitmap()));

        editor.SetPixel(0, 0);
        editor.ClearAll();
        Assert.Equal(0, CountLitPixels(editor.CreateBitmap()));
    }

    [Fact]
    public void ConstructorAndSnapshots_AreIndependentDeepCopies()
    {
        var source = CreateCanvas((2, 3));
        var editor = new EditableMonochromeBitmap(source);

        editor.ClearPixel(2, 3);
        editor.SetPixel(4, 5);
        var snapshot = editor.CreateBitmap();
        var clone = editor.Clone();
        clone.SetPixel(6, 7);

        Assert.True(source[2, 3]);
        Assert.False(source[4, 5]);
        Assert.False(snapshot[6, 7]);
        Assert.True(clone[6, 7]);
    }

    [Fact]
    public void PaintLine_FillsCellsBetweenPointerSamples()
    {
        var editor = new EditableMonochromeBitmap();

        editor.PaintLine(1, 1, 5, 1, isLit: true);

        Assert.All(Enumerable.Range(1, 5), x => Assert.True(editor[x, 1]));
        Assert.False(editor[6, 1]);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(44, 0)]
    [InlineData(0, -1)]
    [InlineData(0, 11)]
    public void PixelOperations_RejectCoordinatesOutside44By11Canvas(int x, int y)
    {
        var editor = new EditableMonochromeBitmap();

        Assert.Throws<ArgumentOutOfRangeException>(() => editor.SetPixel(x, y));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.ClearPixel(x, y));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.TogglePixel(x, y));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => editor.PaintLine(0, 0, x, y, isLit: true));
    }

    [Fact]
    public void FromGraphicViewport_CentresNarrowGraphic()
    {
        var source = new MonochromeBitmap(width: 4, height: 11)
        {
            [0, 1] = true,
            [3, 9] = true,
        };

        var editor = EditableMonochromeBitmap.FromGraphicViewport(source);

        Assert.True(editor[20, 1]);
        Assert.True(editor[23, 9]);
        Assert.Equal(2, CountLitPixels(editor.CreateBitmap()));
    }

    [Fact]
    public void FromGraphicViewport_UsesFirst44ColumnsOfWideGraphic()
    {
        var source = new MonochromeBitmap(width: 60, height: 11)
        {
            [0, 0] = true,
            [43, 10] = true,
            [44, 5] = true,
            [59, 6] = true,
        };

        var editor = EditableMonochromeBitmap.FromGraphicViewport(source);

        Assert.True(editor[0, 0]);
        Assert.True(editor[43, 10]);
        Assert.Equal(2, CountLitPixels(editor.CreateBitmap()));
    }

    [Fact]
    public void EditedCanvas_PacksWithExistingBadgeBitmapFormat()
    {
        var editor = new EditableMonochromeBitmap();
        editor.SetPixel(0, 0);
        editor.SetPixel(43, 10);

        var packed = BadgeBitmapPacker.Pack(editor.CreateBitmap());

        Assert.Equal(66, packed.Length);
        Assert.Equal(0b1000_0000, packed[0]);
        Assert.Equal(0b0001_0000, packed[(5 * 11) + 10]);
    }

    private static MonochromeBitmap CreateCanvas(
        params (int X, int Y)[] litPixels)
    {
        var bitmap = new MonochromeBitmap(
            EditableMonochromeBitmap.CanvasWidth,
            EditableMonochromeBitmap.CanvasHeight);

        foreach (var (x, y) in litPixels)
        {
            bitmap[x, y] = true;
        }

        return bitmap;
    }

    private static int CountLitPixels(MonochromeBitmap bitmap)
    {
        var count = 0;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap[x, y])
                {
                    count++;
                }
            }
        }

        return count;
    }
}
