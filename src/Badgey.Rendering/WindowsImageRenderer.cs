using System.IO;
using System.Windows.Media.Imaging;

namespace Badgey.Rendering;

/// <summary>
/// Decodes a supported Windows image and converts it to an 11-pixel-high
/// monochrome bitmap suitable for the badge rendering pipeline.
/// </summary>
public sealed class WindowsImageRenderer
{
    public const byte LuminanceThreshold = BitmapMonochromeConverter.LuminanceThreshold;

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
        var targetWidth = BitmapMonochromeConverter.CalculateWidthForHeight(
            frame.PixelWidth,
            frame.PixelHeight,
            BadgeBitmapPacker.RequiredHeight);

        return BitmapMonochromeConverter.ResizeToMonochrome(
            frame,
            targetWidth,
            BadgeBitmapPacker.RequiredHeight);
    }

    private static bool IsSupported(Guid containerFormat) =>
        containerFormat == PngContainerFormat
        || containerFormat == JpegContainerFormat
        || containerFormat == BmpContainerFormat;

}
