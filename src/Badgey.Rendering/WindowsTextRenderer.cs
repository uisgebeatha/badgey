using System.Globalization;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Badgey.Rendering;

/// <summary>
/// Renders Windows text and font fallback results to an 11-pixel-high logical bitmap.
/// </summary>
public sealed class WindowsTextRenderer
{
    public const string DefaultFontFamilyName = "MS Gothic";

    private const double LayoutFontSize = 64;
    private const double Dpi = 96;
    private const double TargetBaseline = 9.5;
    private const double PreferredLatinCapHeight = 9.6;
    private const double PreferredJapaneseInkHeight = 10.45;
    private const string LatinCapCalibrationText = "HELLO";
    private const string JapaneseCalibrationText = "日本語テスト";

    private static readonly CultureInfo LayoutCulture = CultureInfo.GetCultureInfo("en-US");

    private readonly Typeface _latinTypeface;
    private readonly Typeface _japaneseTypeface;
    private readonly double _latinFontSize;
    private readonly double _japaneseFontSize;
    private readonly double _targetBaseline;

    public WindowsTextRenderer(string fontFamilyName = DefaultFontFamilyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFamilyName);

        FontFamilyName = fontFamilyName;
        _latinTypeface = new Typeface(
            new FontFamily(fontFamilyName),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);
        _japaneseTypeface = new Typeface(
            new FontFamily(DefaultFontFamilyName),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);
        _targetBaseline = TargetBaseline;
        // Latin caps choose their own fixed size. Descenders then use the rows
        // below the shared baseline without affecting any other message.
        _latinFontSize = CalculateFontSize(
            _latinTypeface,
            LatinCapCalibrationText,
            PreferredLatinCapHeight,
            safetyText: null);
        _japaneseFontSize = CalculateFontSize(
            _japaneseTypeface,
            JapaneseCalibrationText,
            PreferredJapaneseInkHeight,
            JapaneseCalibrationText);
    }

    public string FontFamilyName { get; }

    public MonochromeBitmap Render(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return new MonochromeBitmap(width: 0, BadgeBitmapPacker.RequiredHeight);
        }

        var formattedText = CreateFormattedText(text, hidePixelGlyphs: true);
        return Rasterize(text, formattedText, drawPixelGlyphs: true);
    }

    private MonochromeBitmap Rasterize(
        string text,
        FormattedText formattedText,
        bool drawPixelGlyphs)
    {
        var inkBounds = formattedText.BuildGeometry(new Point()).Bounds;

        if (inkBounds.IsEmpty)
        {
            var blankWidth = Math.Max(
                1,
                checked((int)Math.Ceiling(formattedText.WidthIncludingTrailingWhitespace)));
            return new MonochromeBitmap(blankWidth, BadgeBitmapPacker.RequiredHeight);
        }

        var layoutLeft = Math.Min(0, inkBounds.Left);
        var layoutRight = Math.Max(formattedText.WidthIncludingTrailingWhitespace, inkBounds.Right);
        var width = Math.Max(1, checked((int)Math.Ceiling(layoutRight - layoutLeft)));
        var canvas = new MonochromeBitmap(width, BadgeBitmapPacker.RequiredHeight);
        var target = new RenderTargetBitmap(
            width,
            BadgeBitmapPacker.RequiredHeight,
            Dpi,
            Dpi,
            PixelFormats.Pbgra32);
        var visual = new DrawingVisual();

        TextOptions.SetTextFormattingMode(visual, TextFormattingMode.Display);
        TextOptions.SetTextRenderingMode(visual, TextRenderingMode.Aliased);

        var origin = new Point(
            -layoutLeft,
            _targetBaseline - formattedText.Baseline);

        using (var drawingContext = visual.RenderOpen())
        {
            drawingContext.DrawText(formattedText, origin);
        }

        target.Render(visual);
        CopyPixels(target, canvas);

        if (drawPixelGlyphs)
        {
            DrawPixelGlyphs(text, formattedText, origin, canvas);
        }

        return canvas;
    }

    private FormattedText CreateFormattedText(string text, bool hidePixelGlyphs)
    {
        var formattedText = new FormattedText(
            text,
            LayoutCulture,
            FlowDirection.LeftToRight,
            _latinTypeface,
            _latinFontSize,
            Brushes.White,
            numberSubstitution: null,
            TextFormattingMode.Display,
            pixelsPerDip: 1);

        ApplyJapaneseFormatting(text, formattedText);

        if (hidePixelGlyphs)
        {
            // Keep each symbol in layout for its normal advance, but replace only
            // its visible pixels after WPF has rasterized the rest of the text.
            for (var index = 0; index < text.Length; index++)
            {
                if (AsciiPixelGlyphs.Contains(text[index]))
                {
                    formattedText.SetForegroundBrush(Brushes.Transparent, index, count: 1);
                }
            }
        }

        return formattedText;
    }

    private double CalculateFontSize(
        Typeface typeface,
        string primaryText,
        double preferredInkHeight,
        string? safetyText)
    {
        var primary = CreateMeasurementText(primaryText, typeface);
        var primaryBounds = primary.BuildGeometry(new Point()).Bounds;

        if (primaryBounds.IsEmpty || primaryBounds.Height <= 0)
        {
            return BadgeBitmapPacker.RequiredHeight;
        }

        var preferredScale = preferredInkHeight / primaryBounds.Height;

        if (safetyText is null)
        {
            return LayoutFontSize * preferredScale;
        }

        var safety = CreateMeasurementText(safetyText, typeface);
        var safetyBounds = safety.BuildGeometry(new Point()).Bounds;

        if (safetyBounds.IsEmpty || safetyBounds.Height <= 0)
        {
            return LayoutFontSize * preferredScale;
        }

        var ascent = safety.Baseline - safetyBounds.Top;
        var descent = safetyBounds.Bottom - safety.Baseline;
        var topSafeScale = ascent > 0
            ? _targetBaseline / ascent
            : double.PositiveInfinity;
        var bottomSafeScale = descent > 0
            ? (BadgeBitmapPacker.RequiredHeight - _targetBaseline) / descent
            : double.PositiveInfinity;
        var scale = Math.Min(preferredScale, Math.Min(topSafeScale, bottomSafeScale));

        return LayoutFontSize * scale;
    }

    private static FormattedText CreateMeasurementText(string text, Typeface typeface) =>
        new(
            text,
            LayoutCulture,
            FlowDirection.LeftToRight,
            typeface,
            LayoutFontSize,
            Brushes.White,
            numberSubstitution: null,
            TextFormattingMode.Display,
            pixelsPerDip: 1);

    private void ApplyJapaneseFormatting(string text, FormattedText formattedText)
    {
        var utf16Index = 0;
        var japaneseRangeStart = -1;
        var previousWasJapanese = false;

        foreach (var rune in text.EnumerateRunes())
        {
            var runeLength = rune.Utf16SequenceLength;
            var isJapanese = IsJapanese(rune)
                || (previousWasJapanese && IsInheritedFormattingRune(rune));

            if (isJapanese && japaneseRangeStart < 0)
            {
                japaneseRangeStart = utf16Index;
            }
            else if (!isJapanese && japaneseRangeStart >= 0)
            {
                ApplyJapaneseRange(formattedText, japaneseRangeStart, utf16Index - japaneseRangeStart);
                japaneseRangeStart = -1;
            }

            if (!IsInheritedFormattingRune(rune))
            {
                previousWasJapanese = isJapanese;
            }

            utf16Index += runeLength;
        }

        if (japaneseRangeStart >= 0)
        {
            ApplyJapaneseRange(formattedText, japaneseRangeStart, text.Length - japaneseRangeStart);
        }
    }

    private void ApplyJapaneseRange(FormattedText formattedText, int startIndex, int count)
    {
        formattedText.SetFontTypeface(_japaneseTypeface, startIndex, count);
        formattedText.SetFontSize(_japaneseFontSize, startIndex, count);
    }

    private void DrawPixelGlyphs(
        string text,
        FormattedText formattedText,
        Point origin,
        MonochromeBitmap canvas)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (!AsciiPixelGlyphs.TryGetValue(text[index], out var glyph))
            {
                continue;
            }

            var highlight = formattedText.BuildHighlightGeometry(origin, index, count: 1);

            if (highlight is null || highlight.Bounds.IsEmpty)
            {
                continue;
            }

            var cellLeft = Math.Clamp((int)Math.Floor(highlight.Bounds.Left), 0, canvas.Width - 1);
            var cellRight = Math.Clamp((int)Math.Ceiling(highlight.Bounds.Right), cellLeft + 1, canvas.Width);
            var cellWidth = cellRight - cellLeft;
            var glyphLeft = cellLeft + ((cellWidth - glyph.Width) / 2);

            for (var row = 0; row < glyph.Rows.Length; row++)
            {
                var targetY = glyph.Top + row;

                if ((uint)targetY >= (uint)canvas.Height)
                {
                    continue;
                }

                for (var column = 0; column < glyph.Width; column++)
                {
                    var bit = 1 << (glyph.Width - column - 1);

                    if ((glyph.Rows[row] & bit) == 0)
                    {
                        continue;
                    }

                    var targetX = glyphLeft + column;

                    if (targetX >= cellLeft && targetX < cellRight
                        && (uint)targetX < (uint)canvas.Width)
                    {
                        canvas[targetX, targetY] = true;
                    }
                }
            }
        }
    }

    private static bool IsJapanese(Rune rune)
    {
        var value = rune.Value;
        return value is >= 0x2E80 and <= 0x30FF
            or >= 0x31F0 and <= 0x31FF
            or >= 0x3400 and <= 0x4DBF
            or >= 0x4E00 and <= 0x9FFF
            or >= 0xF900 and <= 0xFAFF
            or >= 0xFF61 and <= 0xFF9F
            or >= 0x20000 and <= 0x2FA1F
            or >= 0x30000 and <= 0x323AF;
    }

    private static bool IsInheritedFormattingRune(Rune rune)
    {
        var category = Rune.GetUnicodeCategory(rune);
        return category is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark
            or UnicodeCategory.Format;
    }

    private static void CopyPixels(BitmapSource source, MonochromeBitmap destination)
    {
        const int bytesPerPixel = 4;
        var stride = checked(destination.Width * bytesPerPixel);
        var pixels = new byte[checked(stride * destination.Height)];
        source.CopyPixels(pixels, stride, offset: 0);

        for (var y = 0; y < destination.Height; y++)
        {
            for (var x = 0; x < destination.Width; x++)
            {
                var alpha = pixels[(y * stride) + (x * bytesPerPixel) + 3];
                destination[x, y] = alpha != 0;
            }
        }
    }

}
