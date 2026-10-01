using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Badgey.Rendering;

/// <summary>
/// Decodes and composes GIF frames, then fits each composed frame into the
/// confirmed badge's 44x11 monochrome canvas.
/// </summary>
public sealed class WindowsGifAnimationRenderer
{
    private const int BytesPerPixel = 4;

    private static readonly Guid GifContainerFormat =
        new GifBitmapEncoder().CodecInfo.ContainerFormat;

    public MonochromeAnimation Render(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read);
        return Render(stream);
    }

    public MonochromeAnimation Render(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (!source.CanRead)
        {
            throw new ArgumentException("The GIF stream must be readable.", nameof(source));
        }

        var decoder = BitmapDecoder.Create(
            source,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);

        if (decoder.CodecInfo.ContainerFormat != GifContainerFormat)
        {
            throw new NotSupportedException("Only GIF animations are supported.");
        }

        if (decoder.Frames.Count == 0)
        {
            throw new ArgumentException("The GIF does not contain a bitmap frame.", nameof(source));
        }

        var frameData = decoder.Frames.Select(CreateFrameData).ToArray();
        var logicalWidth = ReadNonNegativeMetadataValue(
                decoder.Metadata,
                "/logscrdesc/Width")
            ?? frameData.Max(frame => checked(frame.Left + frame.Width));
        var logicalHeight = ReadNonNegativeMetadataValue(
                decoder.Metadata,
                "/logscrdesc/Height")
            ?? frameData.Max(frame => checked(frame.Top + frame.Height));

        if (logicalWidth <= 0 || logicalHeight <= 0)
        {
            throw new ArgumentException(
                "The GIF logical screen must have positive pixel dimensions.",
                nameof(source));
        }

        ValidateFrameBounds(frameData, logicalWidth, logicalHeight, source);

        var stride = checked(logicalWidth * BytesPerPixel);
        var canvas = new byte[checked(stride * logicalHeight)];
        var renderedFrames = new List<MonochromeBitmap>(frameData.Length);
        FrameData? previousFrame = null;
        byte[]? previousRestoreCanvas = null;

        foreach (var frame in frameData)
        {
            ApplyPreviousDisposal(
                canvas,
                stride,
                previousFrame,
                previousRestoreCanvas);

            var currentRestoreCanvas = frame.Disposal == GifDisposal.RestorePrevious
                ? (byte[])canvas.Clone()
                : null;

            OverlayFrame(canvas, stride, frame);

            var composed = BitmapSource.Create(
                logicalWidth,
                logicalHeight,
                dpiX: 96,
                dpiY: 96,
                PixelFormats.Pbgra32,
                palette: null,
                (byte[])canvas.Clone(),
                stride);
            composed.Freeze();

            renderedFrames.Add(
                BitmapMonochromeConverter.FitToCanvas(
                    composed,
                    MonochromeAnimation.FrameWidth,
                    MonochromeAnimation.FrameHeight));

            previousFrame = frame;
            previousRestoreCanvas = currentRestoreCanvas;
        }

        return new MonochromeAnimation(renderedFrames);
    }

    private static FrameData CreateFrameData(BitmapFrame frame)
    {
        var metadata = frame.Metadata as BitmapMetadata;
        var left = ReadNonNegativeMetadataValue(metadata, "/imgdesc/Left") ?? 0;
        var top = ReadNonNegativeMetadataValue(metadata, "/imgdesc/Top") ?? 0;
        var descriptorWidth = ReadNonNegativeMetadataValue(metadata, "/imgdesc/Width")
            ?? frame.PixelWidth;
        var descriptorHeight = ReadNonNegativeMetadataValue(metadata, "/imgdesc/Height")
            ?? frame.PixelHeight;

        if (descriptorWidth <= 0 || descriptorHeight <= 0)
        {
            throw new ArgumentException("A GIF frame must have positive pixel dimensions.");
        }

        if (descriptorWidth != frame.PixelWidth || descriptorHeight != frame.PixelHeight)
        {
            throw new ArgumentException(
                "A GIF frame's decoded dimensions do not match its image descriptor.");
        }

        var disposalValue = ReadNonNegativeMetadataValue(metadata, "/grctlext/Disposal") ?? 0;
        var disposal = disposalValue switch
        {
            2 => GifDisposal.RestoreBackground,
            3 => GifDisposal.RestorePrevious,
            _ => GifDisposal.Keep,
        };

        var converted = new FormatConvertedBitmap(
            frame,
            PixelFormats.Pbgra32,
            destinationPalette: null,
            alphaThreshold: 0);
        converted.Freeze();

        var frameStride = checked(frame.PixelWidth * BytesPerPixel);
        var pixels = new byte[checked(frameStride * frame.PixelHeight)];
        converted.CopyPixels(pixels, frameStride, offset: 0);

        return new FrameData(
            left,
            top,
            descriptorWidth,
            descriptorHeight,
            frameStride,
            disposal,
            pixels);
    }

    private static void ValidateFrameBounds(
        IEnumerable<FrameData> frames,
        int logicalWidth,
        int logicalHeight,
        Stream source)
    {
        foreach (var frame in frames)
        {
            if (checked(frame.Left + frame.Width) > logicalWidth
                || checked(frame.Top + frame.Height) > logicalHeight)
            {
                throw new ArgumentException(
                    "A GIF frame lies outside the logical screen bounds.",
                    nameof(source));
            }
        }
    }

    private static void ApplyPreviousDisposal(
        byte[] canvas,
        int canvasStride,
        FrameData? previousFrame,
        byte[]? restoreCanvas)
    {
        if (previousFrame is null)
        {
            return;
        }

        switch (previousFrame.Disposal)
        {
            case GifDisposal.RestoreBackground:
                ClearRectangle(canvas, canvasStride, previousFrame);
                break;

            case GifDisposal.RestorePrevious when restoreCanvas is not null:
                Buffer.BlockCopy(restoreCanvas, 0, canvas, 0, canvas.Length);
                break;
        }
    }

    private static void ClearRectangle(
        byte[] canvas,
        int canvasStride,
        FrameData frame)
    {
        var bytesToClear = checked(frame.Width * BytesPerPixel);

        for (var y = 0; y < frame.Height; y++)
        {
            var offset = checked(
                ((frame.Top + y) * canvasStride)
                + (frame.Left * BytesPerPixel));
            Array.Clear(canvas, offset, bytesToClear);
        }
    }

    private static void OverlayFrame(
        byte[] canvas,
        int canvasStride,
        FrameData frame)
    {
        for (var y = 0; y < frame.Height; y++)
        {
            for (var x = 0; x < frame.Width; x++)
            {
                var sourceOffset = (y * frame.Stride) + (x * BytesPerPixel);
                var alpha = frame.Pixels[sourceOffset + 3];

                if (alpha == 0)
                {
                    continue;
                }

                var destinationOffset = checked(
                    ((frame.Top + y) * canvasStride)
                    + ((frame.Left + x) * BytesPerPixel));

                if (alpha == byte.MaxValue)
                {
                    Buffer.BlockCopy(
                        frame.Pixels,
                        sourceOffset,
                        canvas,
                        destinationOffset,
                        BytesPerPixel);
                    continue;
                }

                var inverseAlpha = byte.MaxValue - alpha;

                for (var channel = 0; channel < 3; channel++)
                {
                    canvas[destinationOffset + channel] = (byte)(
                        frame.Pixels[sourceOffset + channel]
                        + ((canvas[destinationOffset + channel] * inverseAlpha + 127) / 255));
                }

                canvas[destinationOffset + 3] = (byte)(
                    alpha
                    + ((canvas[destinationOffset + 3] * inverseAlpha + 127) / 255));
            }
        }
    }

    private static int? ReadNonNegativeMetadataValue(
        BitmapMetadata? metadata,
        string query)
    {
        if (metadata is null)
        {
            return null;
        }

        try
        {
            var value = metadata.GetQuery(query);
            var converted = value switch
            {
                byte number => number,
                ushort number => number,
                uint number when number <= int.MaxValue => (int)number,
                sbyte number when number >= 0 => number,
                short number when number >= 0 => number,
                int number when number >= 0 => number,
                long number when number is >= 0 and <= int.MaxValue => (int)number,
                _ => -1,
            };

            return converted >= 0 ? converted : null;
        }
        catch (Exception exception) when (
            exception is ArgumentException
            or InvalidOperationException
            or NotSupportedException
            or COMException)
        {
            return null;
        }
    }

    private sealed record FrameData(
        int Left,
        int Top,
        int Width,
        int Height,
        int Stride,
        GifDisposal Disposal,
        byte[] Pixels);

    private enum GifDisposal
    {
        Keep,
        RestoreBackground,
        RestorePrevious,
    }
}
