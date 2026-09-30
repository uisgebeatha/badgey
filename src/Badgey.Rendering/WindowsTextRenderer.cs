using System.Globalization;
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
    private const double VerticalInset = 0.25;
    private const double TargetInkHeight = BadgeBitmapPacker.RequiredHeight - (2 * VerticalInset);

    private static readonly CultureInfo LayoutCulture = CultureInfo.GetCultureInfo("en-US");

    private readonly Typeface _typeface;

    public WindowsTextRenderer(string fontFamilyName = DefaultFontFamilyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fontFamilyName);

        FontFamilyName = fontFamilyName;
        _typeface = new Typeface(
            new FontFamily(fontFamilyName),
            FontStyles.Normal,
            FontWeights.Normal,
            FontStretches.Normal);
    }

    public string FontFamilyName { get; }

    public MonochromeBitmap Render(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (text.Length == 0)
        {
            return new MonochromeBitmap(width: 0, BadgeBitmapPacker.RequiredHeight);
        }

        var measurementText = CreateFormattedText(text, LayoutFontSize);
        var measurementBounds = measurementText.BuildGeometry(new Point()).Bounds;
        var fittedFontSize = measurementBounds.IsEmpty || measurementBounds.Height <= 0
            ? TargetInkHeight
            : LayoutFontSize * TargetInkHeight / measurementBounds.Height;
        var formattedText = CreateFormattedText(text, fittedFontSize);
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

        using (var drawingContext = visual.RenderOpen())
        {
            var origin = new Point(
                -layoutLeft,
                VerticalInset - inkBounds.Top);
            drawingContext.DrawText(formattedText, origin);
        }

        target.Render(visual);
        CopyPixels(target, canvas);
        return canvas;
    }

    private FormattedText CreateFormattedText(string text, double fontSize) =>
        new(
            text,
            LayoutCulture,
            FlowDirection.LeftToRight,
            _typeface,
            fontSize,
            Brushes.White,
            numberSubstitution: null,
            TextFormattingMode.Display,
            pixelsPerDip: 1);

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
