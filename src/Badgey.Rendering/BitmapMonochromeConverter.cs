using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Badgey.Rendering;

/// <summary>
/// Shared WPF bitmap scaling and monochrome conversion used by static images
/// and decoded animation frames.
/// </summary>
internal static class BitmapMonochromeConverter
{
    public const byte LuminanceThreshold = 128;

    private const double Dpi = 96;
    private const int BytesPerPixel = 4;

    public static int CalculateWidthForHeight(
        int sourceWidth,
        int sourceHeight,
        int targetHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentException("The image must have positive pixel dimensions.");
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(targetHeight, 1);

        var scaledWidth = checked((long)sourceWidth * targetHeight);
        var roundedWidth = (scaledWidth + (sourceHeight / 2L)) / sourceHeight;

        if (roundedWidth > int.MaxValue)
        {
            throw new ArgumentException("The image aspect ratio produces an output that is too wide.");
        }

        return Math.Max(1, (int)roundedWidth);
    }

    public static MonochromeBitmap ResizeToMonochrome(
        BitmapSource source,
        int targetWidth,
        int targetHeight)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(targetWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(targetHeight, 1);

        BitmapSource raster;

        if (source.PixelWidth == targetWidth && source.PixelHeight == targetHeight)
        {
            var converted = new FormatConvertedBitmap(
                source,
                PixelFormats.Pbgra32,
                destinationPalette: null,
                alphaThreshold: 0);
            converted.Freeze();
            raster = converted;
        }
        else
        {
            raster = Render(
                source,
                targetWidth,
                targetHeight,
                new Rect(0, 0, targetWidth, targetHeight));
        }

        return ConvertToMonochrome(raster);
    }

    public static MonochromeBitmap FitToCanvas(
        BitmapSource source,
        int canvasWidth,
        int canvasHeight)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(canvasWidth, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(canvasHeight, 1);

        if (source.PixelWidth <= 0 || source.PixelHeight <= 0)
        {
            throw new ArgumentException("The image must have positive pixel dimensions.", nameof(source));
        }

        var (targetWidth, targetHeight) = CalculateFitSize(
            source.PixelWidth,
            source.PixelHeight,
            canvasWidth,
            canvasHeight);
        var destinationLeft = (canvasWidth - targetWidth) / 2;
        var destinationTop = (canvasHeight - targetHeight) / 2;
        var raster = Render(
            source,
            canvasWidth,
            canvasHeight,
            new Rect(destinationLeft, destinationTop, targetWidth, targetHeight));

        return ConvertToMonochrome(raster);
    }

    private static (int Width, int Height) CalculateFitSize(
        int sourceWidth,
        int sourceHeight,
        int canvasWidth,
        int canvasHeight)
    {
        int targetWidth;
        int targetHeight;

        if (checked((long)sourceWidth * canvasHeight)
            > checked((long)canvasWidth * sourceHeight))
        {
            targetWidth = canvasWidth;
            targetHeight = RoundScaledDimension(sourceHeight, canvasWidth, sourceWidth);
        }
        else
        {
            targetHeight = canvasHeight;
            targetWidth = RoundScaledDimension(sourceWidth, canvasHeight, sourceHeight);
        }

        return (
            Math.Clamp(targetWidth, 1, canvasWidth),
            Math.Clamp(targetHeight, 1, canvasHeight));
    }

    private static int RoundScaledDimension(
        int sourceDimension,
        int scaleNumerator,
        int scaleDenominator)
    {
        var scaled = checked((long)sourceDimension * scaleNumerator);
        return checked((int)((scaled + (scaleDenominator / 2L)) / scaleDenominator));
    }

    private static BitmapSource Render(
        BitmapSource source,
        int canvasWidth,
        int canvasHeight,
        Rect destination)
    {
        var target = new RenderTargetBitmap(
            canvasWidth,
            canvasHeight,
            Dpi,
            Dpi,
            PixelFormats.Pbgra32);
        var visual = new DrawingVisual();

        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);

        using (var drawingContext = visual.RenderOpen())
        {
            drawingContext.DrawImage(source, destination);
        }

        target.Render(visual);
        target.Freeze();
        return target;
    }

    private static MonochromeBitmap ConvertToMonochrome(BitmapSource source)
    {
        var result = new MonochromeBitmap(source.PixelWidth, source.PixelHeight);
        var stride = checked(source.PixelWidth * BytesPerPixel);
        var pixels = new byte[checked(stride * source.PixelHeight)];
        source.CopyPixels(pixels, stride, offset: 0);

        for (var y = 0; y < result.Height; y++)
        {
            for (var x = 0; x < result.Width; x++)
            {
                var pixelOffset = (y * stride) + (x * BytesPerPixel);
                var blue = pixels[pixelOffset];
                var green = pixels[pixelOffset + 1];
                var red = pixels[pixelOffset + 2];
                var alpha = pixels[pixelOffset + 3];

                // Pbgra32 channels are already multiplied by alpha. Transparent
                // pixels therefore remain black/unlit and partially transparent
                // edges retain the same behaviour as static image rendering.
                var luminance = ((54 * red) + (183 * green) + (19 * blue) + 128) >> 8;
                result[x, y] = alpha != 0 && luminance >= LuminanceThreshold;
            }
        }

        return result;
    }
}
