using Badgey.Rendering;

namespace Badgey.Rendering.Tests;

public class WindowsTextRendererTests
{
    private readonly WindowsTextRenderer _renderer = new();

    [Fact]
    public void DefaultFont_IsMsGothic()
    {
        Assert.Equal("MS Gothic", _renderer.FontFamilyName);
    }

    [Fact]
    public void Render_EmptyTextReturnsEmptyElevenPixelCanvas()
    {
        var bitmap = _renderer.Render(string.Empty);

        Assert.Equal(0, bitmap.Width);
        Assert.Equal(11, bitmap.Height);
        Assert.Empty(BadgeBitmapPacker.Pack(bitmap));
    }

    [Fact]
    public void Render_RejectsNullText()
    {
        Assert.Throws<ArgumentNullException>(() => _renderer.Render(null!));
    }

    [Fact]
    public void Render_LatinTextProducesLitPixels()
    {
        var bitmap = _renderer.Render("Badgey");

        Assert.True(bitmap.Width > 0);
        Assert.True(HasLitPixel(bitmap));
    }

    [Fact]
    public void Render_JapaneseTextProducesLitPixels()
    {
        var bitmap = _renderer.Render("日本語");
        var firstCharacter = BadgeBitmapPacker.Pack(_renderer.Render("日"));
        var secondCharacter = BadgeBitmapPacker.Pack(_renderer.Render("本"));

        Assert.True(bitmap.Width > 0);
        Assert.True(HasLitPixel(bitmap));
        Assert.False(firstCharacter.SequenceEqual(secondCharacter));
    }

    [Theory]
    [InlineData("H")]
    [InlineData("g")]
    [InlineData("日")]
    [InlineData("語")]
    public void Render_RepresentativeGlyphUsesMostRowsWithoutClipping(string glyph)
    {
        var bitmap = _renderer.Render(glyph);

        var (top, bottom) = GetLitRowBounds(bitmap);

        Assert.InRange(top, 0, 1);
        Assert.InRange(bottom, 9, 10);
        Assert.InRange((bottom - top) + 1, 9, 11);
    }

    [Fact]
    public void Render_PreservesCharacterAdvanceSpacing()
    {
        var oneCharacter = _renderer.Render("A");
        var twoCharacters = _renderer.Render("AA");

        Assert.True(twoCharacters.Width > oneCharacter.Width);
    }

    [Fact]
    public void Render_AllowsUserSpecifiedWindowsFont()
    {
        var renderer = new WindowsTextRenderer("Arial");

        var bitmap = renderer.Render("A");

        Assert.True(HasLitPixel(bitmap));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A")]
    [InlineData("Badgey is a longer message")]
    [InlineData("日本語テスト")]
    public void Render_OutputHeightIsAlwaysElevenPixels(string text)
    {
        Assert.Equal(11, _renderer.Render(text).Height);
    }

    [Theory]
    [InlineData("A")]
    [InlineData("Badgey")]
    [InlineData("日本語")]
    public void Pack_OutputLengthIsWholeByteColumns(string text)
    {
        var bitmap = _renderer.Render(text);

        var packed = BadgeBitmapPacker.Pack(bitmap);

        Assert.Equal(((bitmap.Width + 7) / 8) * 11, packed.Length);
        Assert.Equal(0, packed.Length % 11);
    }

    [Fact]
    public void Pack_UsesExpectedBitAndByteColumnOrdering()
    {
        var bitmap = new MonochromeBitmap(width: 9, height: 11)
        {
            [0, 0] = true,
            [7, 0] = true,
            [8, 10] = true,
        };

        var packed = BadgeBitmapPacker.Pack(bitmap);

        Assert.Equal(22, packed.Length);
        Assert.Equal(0x81, packed[0]);
        Assert.Equal(0x80, packed[21]);
        Assert.Equal(2, packed.Count(value => value != 0));
    }

    [Fact]
    public void Render_KnownTextIsDeterministic()
    {
        var first = _renderer.Render("Badgey");
        var second = _renderer.Render("Badgey");

        Assert.Equal(first.Width, second.Width);
        Assert.Equal(BadgeBitmapPacker.Pack(first), BadgeBitmapPacker.Pack(second));
    }

    private static bool HasLitPixel(MonochromeBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap[x, y])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static (int Top, int Bottom) GetLitRowBounds(MonochromeBitmap bitmap)
    {
        var top = bitmap.Height;
        var bottom = -1;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (!bitmap[x, y])
                {
                    continue;
                }

                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        Assert.True(bottom >= top, "Expected at least one lit pixel.");
        return (top, bottom);
    }
}
