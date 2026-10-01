using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Badgey.Rendering;

/// <summary>
/// Decodes a supported Windows image and converts it to an 11-pixel-high
/// monochrome bitmap suitable for the badge rendering pipeline.
/// </summary>
public sealed class WindowsImageRenderer
{
    public const byte LuminanceThreshold = 128;

    private const double Dpi = 96;
    private const int BytesPerPixel = 4;

    private static readonly Guid PngContainerFormat =
        new PngBitmapEncoder().CodecInfo.ContainerFormat;
    private static readonly Guid JpegContainerFormat =
        new JpegBitmapEncoder().CodecInfo.ContainerFormat;
    private static readonly Guid BmpContainerFormat =
        new BmpBitmapEncoder().CodecInfo.ContainerFormat;

    public MonochromeBitmap Render(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return Render(stream);
    }

    public MonochromeBitmap Render(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.CanRead)
        {
            throw new ArgumentException("The image stream must be readable.", nameof(source));
        }

        var decoder = BitmapDecoder.Create(
            source,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        if (!IsSupported(decoder.CodecInfo.ContainerFormat))
        {
            throw new NotSupportedException("Only PNG, JPEG, and BMP images are supported.");
        }

        if (decoder.Frames.Count == 0)
        {
            throw new ArgumentException("The image does not contain a bitmap frame.", nameof(source));
        }

        var frame = decoder.Frames[0];
        var targetWidth = CalculateTargetWidth(frame.PixelWidth, frame.PixelHeight);
        var raster = ResizeToBadgeHeight(frame, targetWidth);

        return ConvertToMonochrome(raster);
    }

    private static bool IsSupported(Guid containerFormat) =>
        containerFormat == PngContainerFormat
        || containerFormat == JpegContainerFormat
        || containerFormat == BmpContainerFormat;

    private static int CalculateTargetWidth(int sourceWidth, int sourceHeight)
    {
        if (sourceWidth <= 0 || sourceHeight <= 0)
        {
            throw new ArgumentException("The image must have positive pixel dimensions.");
        }

        var scaledWidth = checked((long)sourceWidth * BadgeBitmapPacker.RequiredHeight);
        var roundedWidth = (scaledWidth + (sourceHeight / 2L)) / sourceHeight;

        if (roundedWidth > int.MaxValue)
        {
            throw new ArgumentException("The image aspect ratio produces an output that is too wide.");
        }

        return Math.Max(1, (int)roundedWidth);
    }

    private static BitmapSource ResizeToBadgeHeight(BitmapSource source, int targetWidth)
    {
        if (source.PixelWidth == targetWidth
            && source.PixelHeight == BadgeBitmapPacker.RequiredHeight)
        {
            var converted = new FormatConvertedBitmap(
                source,
                PixelFormats.Pbgra32,
                destinationPalette: null,
                alphaThreshold: 0);
            converted.Freeze();
            return converted;
        }

        var target = new RenderTargetBitmap(
            targetWidth,
            BadgeBitmapPacker.RequiredHeight,
            Dpi,
            Dpi,
            PixelFormats.Pbgra32);
        var visual = new DrawingVisual();

        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);

        using (var drawingContext = visual.RenderOpen())
        {
            drawingContext.DrawImage(
                source,
                new Rect(0, 0, targetWidth, BadgeBitmapPacker.RequiredHeight));
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

                // The Pbgra32 colour channels are already multiplied by alpha,
                // which treats transparent pixels as black/unlit and gives
                // partially transparent edges a natural contribution.
                var luminance = ((54 * red) + (183 * green) + (19 * blue) + 128) >> 8;
                result[x, y] = alpha != 0 && luminance >= LuminanceThreshold;
            }
        }

        return result;
    }
}
