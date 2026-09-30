using Badgey.Rendering;

namespace Badgey.Rendering.Tests;

public class WindowsTextRendererTests
{
    private const string PixelSymbols = "\\/_-=+.,:;'\"()[]{}<>!?|~^`";

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
    [InlineData("HELLO")]
    [InlineData("yellow")]
    [InlineData("gypsy")]
    [InlineData("ABCxyz")]
    [InlineData("日本語テスト")]
    [InlineData("ABC日本語123")]
    [InlineData(@"¯\_(ツ)_/¯")]
    [InlineData(@"\\\///")]
    [InlineData(@"\")]
    [InlineData("/")]
    public void Render_RepresentativeTextUsesBalancedRowsWithoutClipping(string text)
    {
        var bitmap = _renderer.Render(text);

        var (top, bottom) = GetLitRowBounds(bitmap);

        Assert.Equal(11, bitmap.Height);
        Assert.True(bitmap.Width > 0);
        Assert.InRange(top, 0, 4);
        Assert.InRange(bottom, 8, 10);
        Assert.InRange((bottom - top) + 1, 7, 11);
    }

    [Theory]
    [InlineData("HELLO", 9)]
    [InlineData("日本語テスト", 10)]
    public void Render_CapHeightAndJapaneseUseMostAvailableRows(string text, int minimumHeight)
    {
        var (top, bottom) = GetLitRowBounds(_renderer.Render(text));

        Assert.InRange(top, 0, 1);
        Assert.InRange(bottom, 8, 10);
        Assert.InRange((bottom - top) + 1, minimumHeight, 11);
    }

    [Fact]
    public void Render_LatinXHeightIsSmallerThanCapHeightAndSharesItsBaseline()
    {
        var capBounds = GetLitRowBounds(_renderer.Render("HELLO"));
        var xHeightBounds = GetLitRowBounds(_renderer.Render("xxx"));
        var capHeight = (capBounds.Bottom - capBounds.Top) + 1;
        var xHeight = (xHeightBounds.Bottom - xHeightBounds.Top) + 1;

        Assert.InRange(capHeight, 9, 10);
        Assert.InRange(xHeight, 6, 8);
        Assert.True(xHeight < capHeight);
        Assert.InRange(Math.Abs(xHeightBounds.Bottom - capBounds.Bottom), 0, 1);
    }

    [Fact]
    public void Render_DescendersDoNotRescaleSharedUppercaseGlyphs()
    {
        var uppercaseOnly = _renderer.Render("HELLO");
        var withDescenders = _renderer.Render("HELLO gypsy");

        Assert.True(withDescenders.Width > uppercaseOnly.Width);

        for (var y = 0; y < uppercaseOnly.Height; y++)
        {
            for (var x = 0; x < uppercaseOnly.Width; x++)
            {
                Assert.Equal(uppercaseOnly[x, y], withDescenders[x, y]);
            }
        }
    }

    [Fact]
    public void Render_DescendersUseRowsBelowUppercaseBaseline()
    {
        var uppercaseBounds = GetLitRowBounds(_renderer.Render("HELLO"));

        foreach (var text in new[] { "yellow", "gypsy" })
        {
            var descenderBounds = GetLitRowBounds(_renderer.Render(text));

            Assert.True(descenderBounds.Bottom > uppercaseBounds.Bottom);
            Assert.InRange(Math.Abs(descenderBounds.Top - uppercaseBounds.Top), 0, 3);
        }
    }

    [Fact]
    public void Render_BackslashUsesDiagonalGlyphInsteadOfYenGlyph()
    {
        var backslash = _renderer.Render(@"\");
        var yen = _renderer.Render("¥");
        var (top, bottom) = GetLitRowBounds(backslash);
        var middle = (top + bottom) / 2;

        Assert.False(
            BadgeBitmapPacker.Pack(backslash).SequenceEqual(BadgeBitmapPacker.Pack(yen)));
        Assert.True(
            GetAverageLitColumn(backslash, top, middle)
            < GetAverageLitColumn(backslash, middle + 1, bottom));
        Assert.True(GetMaximumLitPixelsPerRow(backslash) < GetMaximumLitPixelsPerRow(yen));
    }

    [Fact]
    public void Render_SlashAndBackslashAreMatchingSinglePixelDiagonals()
    {
        var backslash = _renderer.Render(@"\");
        var slash = _renderer.Render("/");
        var backslashBounds = GetLitBounds(backslash);
        var slashBounds = GetLitBounds(slash);

        Assert.Equal(slashBounds.Width, backslashBounds.Width);
        Assert.Equal(slashBounds.Height, backslashBounds.Height);
        Assert.Equal(1, GetMaximumLitPixelsPerRow(backslash));
        Assert.Equal(1, GetMaximumLitPixelsPerRow(slash));
        Assert.Equal(backslashBounds.Height, CountLitPixels(backslash));
        Assert.Equal(slashBounds.Height, CountLitPixels(slash));

        var backslashMiddle = backslashBounds.Top + (backslashBounds.Height / 2);
        var slashMiddle = slashBounds.Top + (slashBounds.Height / 2);
        Assert.True(
            GetAverageLitColumn(backslash, backslashBounds.Top, backslashMiddle - 1)
            < GetAverageLitColumn(backslash, backslashMiddle, backslashBounds.Bottom));
        Assert.True(
            GetAverageLitColumn(slash, slashBounds.Top, slashMiddle - 1)
            > GetAverageLitColumn(slash, slashMiddle, slashBounds.Bottom));
    }

    [Fact]
    public void Render_CustomBackslashIsPreservedInsideMixedText()
    {
        var backslashText = BadgeBitmapPacker.Pack(_renderer.Render(@"¯\_(ツ)_/¯"));
        var yenText = BadgeBitmapPacker.Pack(_renderer.Render("¯¥_(ツ)_/¯"));

        Assert.False(backslashText.SequenceEqual(yenText));
    }

    [Fact]
    public void Render_CustomBackslashDoesNotMovePrecedingGlyphs()
    {
        var prefix = _renderer.Render("HELLO");
        var withBackslash = _renderer.Render(@"HELLO \");

        for (var y = 0; y < prefix.Height; y++)
        {
            for (var x = 0; x < prefix.Width; x++)
            {
                Assert.Equal(prefix[x, y], withBackslash[x, y]);
            }
        }
    }

    [Fact]
    public void Render_AllRequiredPixelSymbolsAreVisibleAndUseThinStrokes()
    {
        foreach (var symbol in PixelSymbols)
        {
            var bitmap = _renderer.Render(symbol.ToString());

            Assert.Equal(11, bitmap.Height);
            Assert.True(bitmap.Width > 0);
            Assert.True(HasLitPixel(bitmap));
            Assert.False(HasFilledTwoByTwoBlock(bitmap));
        }
    }

    [Fact]
    public void Render_UnderscoreIsAVisibleBottomRowStroke()
    {
        var bitmap = _renderer.Render("_");
        var bounds = GetLitBounds(bitmap);

        Assert.Equal(10, bounds.Top);
        Assert.Equal(10, bounds.Bottom);
        Assert.True(bounds.Width >= 3);

        for (var x = bounds.Left; x <= bounds.Right; x++)
        {
            Assert.True(bitmap[x, 10]);
        }
    }

    [Theory]
    [InlineData('[', ']')]
    [InlineData('{', '}')]
    [InlineData('(', ')')]
    [InlineData('<', '>')]
    [InlineData('\\', '/')]
    public void Render_PairedPixelGlyphsAreHorizontalMirrors(char left, char right)
    {
        AssertHorizontalMirrors(
            _renderer.Render(left.ToString()),
            _renderer.Render(right.ToString()));
    }

    [Theory]
    [InlineData('[')]
    [InlineData(']')]
    [InlineData('{')]
    [InlineData('}')]
    [InlineData('(')]
    [InlineData(')')]
    public void Render_BracketsAndParenthesesKeepVisibleTopsAndBottoms(char symbol)
    {
        var bounds = GetLitBounds(_renderer.Render(symbol.ToString()));

        Assert.Equal(0, bounds.Top);
        Assert.Equal(10, bounds.Bottom);
    }

    [Fact]
    public void Render_DoubleQuoteContainsTwoSeparateQuoteMarks()
    {
        var doubleQuote = _renderer.Render("\"");
        var apostrophe = _renderer.Render("'");
        var bounds = GetLitBounds(doubleQuote);

        Assert.Equal(2, CountConnectedComponents(doubleQuote));
        Assert.Equal(1, CountConnectedComponents(apostrophe));
        Assert.True(bounds.Width >= 3);
        Assert.True(bounds.Bottom <= 2);
    }

    [Fact]
    public void Render_BasicPunctuationHasExpectedPixelStructure()
    {
        var period = _renderer.Render(".");
        var colon = _renderer.Render(":");
        var equals = _renderer.Render("=");
        var verticalBar = _renderer.Render("|");

        Assert.Equal(1, CountLitPixels(period));
        Assert.Equal(2, CountLitPixels(colon));
        Assert.Equal(2, CountOccupiedRows(equals));
        Assert.Equal(1, GetLitBounds(verticalBar).Width);
        Assert.Equal(11, GetLitBounds(verticalBar).Height);
    }

    [Fact]
    public void Render_PixelSymbolsPreserveCharacterAdvances()
    {
        foreach (var symbol in PixelSymbols)
        {
            var single = _renderer.Render(symbol.ToString());
            var doubled = _renderer.Render(new string(symbol, 2));

            Assert.True(doubled.Width > single.Width);
        }
    }

    [Theory]
    [InlineData(@"¯\_(ツ)_/¯")]
    [InlineData(@"\\\///")]
    [InlineData("[]{}()")]
    [InlineData("\"Hello\"")]
    [InlineData("_test_")]
    [InlineData("ABC_日本語_123")]
    [InlineData("!?.,:;+-=")]
    public void Render_SymbolRegressionStringsAreVisibleAndDeterministic(string text)
    {
        var first = _renderer.Render(text);
        var second = _renderer.Render(text);

        Assert.Equal(11, first.Height);
        Assert.True(first.Width > 0);
        Assert.True(HasLitPixel(first));
        Assert.Equal(first.Width, second.Width);
        Assert.Equal(BadgeBitmapPacker.Pack(first), BadgeBitmapPacker.Pack(second));
    }

    [Fact]
    public void Render_UnderscoresDoNotAlterMixedLatinJapaneseGlyphs()
    {
        var latinPrefix = _renderer.Render("ABC");
        var mixed = _renderer.Render("ABC_日本語_123");

        for (var y = 0; y < latinPrefix.Height; y++)
        {
            for (var x = 0; x < latinPrefix.Width; x++)
            {
                Assert.Equal(latinPrefix[x, y], mixed[x, y]);
            }
        }

        var (top, bottom) = GetLitRowBounds(mixed);
        Assert.InRange((bottom - top) + 1, 10, 11);
    }

    [Fact]
    public void Render_MixedTextPreservesLatinPrefixAndJapaneseHeight()
    {
        var latin = _renderer.Render("ABC");
        var japanese = _renderer.Render("日本語");
        var mixed = _renderer.Render("ABC日本語123");

        Assert.True(mixed.Width > latin.Width);

        for (var y = 0; y < latin.Height; y++)
        {
            for (var x = 0; x < latin.Width; x++)
            {
                Assert.Equal(latin[x, y], mixed[x, y]);
            }
        }

        var (_, japaneseBottom) = GetLitRowBounds(japanese);
        var (mixedTop, mixedBottom) = GetLitRowBounds(mixed);
        Assert.InRange((mixedBottom - mixedTop) + 1, 10, 11);
        Assert.InRange(Math.Abs(mixedBottom - japaneseBottom), 0, 1);
    }

    [Fact]
    public void Render_CustomLatinFontStillUsesMsGothicForJapanese()
    {
        var defaultJapanese = _renderer.Render("日本語テスト");
        var customFontJapanese = new WindowsTextRenderer("Arial").Render("日本語テスト");

        Assert.Equal(defaultJapanese.Width, customFontJapanese.Width);
        Assert.Equal(
            BadgeBitmapPacker.Pack(defaultJapanese),
            BadgeBitmapPacker.Pack(customFontJapanese));
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

    private static InkBounds GetLitBounds(MonochromeBitmap bitmap)
    {
        var left = bitmap.Width;
        var top = bitmap.Height;
        var right = -1;
        var bottom = -1;

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (!bitmap[x, y])
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        Assert.True(right >= left && bottom >= top, "Expected at least one lit pixel.");
        return new InkBounds(left, top, right, bottom);
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

    private static int CountOccupiedRows(MonochromeBitmap bitmap)
    {
        var count = 0;

        for (var y = 0; y < bitmap.Height; y++)
        {
            if (Enumerable.Range(0, bitmap.Width).Any(x => bitmap[x, y]))
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasFilledTwoByTwoBlock(MonochromeBitmap bitmap)
    {
        for (var y = 0; y < bitmap.Height - 1; y++)
        {
            for (var x = 0; x < bitmap.Width - 1; x++)
            {
                if (bitmap[x, y]
                    && bitmap[x + 1, y]
                    && bitmap[x, y + 1]
                    && bitmap[x + 1, y + 1])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static int CountConnectedComponents(MonochromeBitmap bitmap)
    {
        var visited = new bool[bitmap.Width * bitmap.Height];
        var components = 0;
        var pending = new Queue<(int X, int Y)>();
        ReadOnlySpan<(int X, int Y)> neighbours =
        [
            (-1, 0),
            (1, 0),
            (0, -1),
            (0, 1),
        ];

        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var index = (y * bitmap.Width) + x;

                if (!bitmap[x, y] || visited[index])
                {
                    continue;
                }

                components++;
                visited[index] = true;
                pending.Enqueue((x, y));

                while (pending.TryDequeue(out var pixel))
                {
                    foreach (var neighbour in neighbours)
                    {
                        var nextX = pixel.X + neighbour.X;
                        var nextY = pixel.Y + neighbour.Y;

                        if ((uint)nextX >= (uint)bitmap.Width
                            || (uint)nextY >= (uint)bitmap.Height)
                        {
                            continue;
                        }

                        var nextIndex = (nextY * bitmap.Width) + nextX;

                        if (!bitmap[nextX, nextY] || visited[nextIndex])
                        {
                            continue;
                        }

                        visited[nextIndex] = true;
                        pending.Enqueue((nextX, nextY));
                    }
                }
            }
        }

        return components;
    }

    private static void AssertHorizontalMirrors(MonochromeBitmap left, MonochromeBitmap right)
    {
        var leftBounds = GetLitBounds(left);
        var rightBounds = GetLitBounds(right);

        Assert.Equal(leftBounds.Width, rightBounds.Width);
        Assert.Equal(leftBounds.Height, rightBounds.Height);

        for (var y = 0; y < leftBounds.Height; y++)
        {
            for (var x = 0; x < leftBounds.Width; x++)
            {
                Assert.Equal(
                    left[leftBounds.Left + x, leftBounds.Top + y],
                    right[rightBounds.Right - x, rightBounds.Top + y]);
            }
        }
    }

    private static double GetAverageLitColumn(MonochromeBitmap bitmap, int firstRow, int lastRow)
    {
        var columnTotal = 0;
        var pixelCount = 0;

        for (var y = firstRow; y <= lastRow; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                if (!bitmap[x, y])
                {
                    continue;
                }

                columnTotal += x;
                pixelCount++;
            }
        }

        Assert.True(pixelCount > 0, "Expected at least one lit pixel in the row range.");
        return (double)columnTotal / pixelCount;
    }

    private static int GetMaximumLitPixelsPerRow(MonochromeBitmap bitmap)
    {
        var maximum = 0;

        for (var y = 0; y < bitmap.Height; y++)
        {
            var rowCount = 0;

            for (var x = 0; x < bitmap.Width; x++)
            {
                if (bitmap[x, y])
                {
                    rowCount++;
                }
            }

            maximum = Math.Max(maximum, rowCount);
        }

        return maximum;
    }

    private readonly record struct InkBounds(int Left, int Top, int Right, int Bottom)
    {
        public int Width => (Right - Left) + 1;

        public int Height => (Bottom - Top) + 1;
    }
}
