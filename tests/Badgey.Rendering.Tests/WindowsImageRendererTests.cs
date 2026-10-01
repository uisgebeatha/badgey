using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Badgey.Rendering;

namespace Badgey.Rendering.Tests;

public class WindowsImageRendererTests
{
    private readonly WindowsImageRenderer _renderer = new();

    [Theory]
    [InlineData(ImageContainer.Png)]
    [InlineData(ImageContainer.Jpeg)]
    [InlineData(ImageContainer.Bmp)]
    public void Render_SupportedFormatsProduceMonochromeBitmap(ImageContainer container)
    {
        using var image = EncodeImage(
            width: 11,
            height: 11,
            container,
            (x, _) => x < 5 ? TestPixel.Black : TestPixel.White);

        var bitmap = _renderer.Render(image);

        Assert.Equal(11, bitmap.Width);
        Assert.Equal(11, bitmap.Height);
        Assert.False(bitmap[0, 0]);
        Assert.True(bitmap[10, 0]);
    }

    [Fact]
    public void Render_TransparentPngPixelsRemainUnlit()
    {
        using var image = EncodeImage(
            width: 3,
            height: 11,
            ImageContainer.Png,
            (x, _) => x switch
            {
                0 => TestPixel.TransparentWhite,
                1 => TestPixel.White,
                _ => TestPixel.Black,
            });

        var bitmap = _renderer.Render(image);

        for (var y = 0; y < bitmap.Height; y++)
        {
            Assert.False(bitmap[0, y]);
            Assert.True(bitmap[1, y]);
            Assert.False(bitmap[2, y]);
        }
    }

    [Fact]
    public void Render_TransparentPixelBecomesLitWhenRenderedBitmapIsInverted()
    {
        using var image = EncodeImage(
            width: 1,
            height: 11,
            ImageContainer.Png,
            (_, _) => TestPixel.TransparentWhite);

        var rendered = _renderer.Render(image);
        var inverted = MonochromeBitmapTransforms.Invert(rendered);

        for (var y = 0; y < rendered.Height; y++)
        {
            Assert.False(rendered[0, y]);
            Assert.True(inverted[0, y]);
        }
    }

    [Fact]
    public void Render_TransparentPngPixelsRemainUnlitAfterScaling()
    {
        using var image = EncodeImage(
            width: 4,
            height: 22,
            ImageContainer.Png,
            (x, _) => x < 2 ? TestPixel.TransparentWhite : TestPixel.White);

        var bitmap = _renderer.Render(image);

        Assert.Equal(2, bitmap.Width);

        for (var y = 0; y < bitmap.Height; y++)
        {
            Assert.False(bitmap[0, y]);
            Assert.True(bitmap[1, y]);
        }
    }

    [Theory]
    [InlineData(20, 10, 22)]
    [InlineData(10, 20, 6)]
    [InlineData(44, 11, 44)]
    [InlineData(100, 20, 55)]
    public void Render_PreservesAspectRatioAtElevenPixelHeight(
        int sourceWidth,
        int sourceHeight,
        int expectedWidth)
    {
        using var image = EncodeImage(
            sourceWidth,
            sourceHeight,
            ImageContainer.Png,
            (_, _) => TestPixel.White);

        var bitmap = _renderer.Render(image);

        Assert.Equal(expectedWidth, bitmap.Width);
        Assert.Equal(11, bitmap.Height);
    }

    [Fact]
    public void Render_UsesFixedLuminanceThreshold()
    {
        using var image = EncodeImage(
            width: 2,
            height: 11,
            ImageContainer.Png,
            (x, _) => TestPixel.Gray(x == 0 ? (byte)127 : (byte)128));

        var bitmap = _renderer.Render(image);

        Assert.False(bitmap[0, 0]);
        Assert.True(bitmap[1, 0]);
    }

    [Fact]
    public void Render_NarrowImageRetainsNaturalWidth()
    {
        using var image = EncodeImage(
            width: 4,
            height: 11,
            ImageContainer.Bmp,
            (_, _) => TestPixel.White);

        var bitmap = _renderer.Render(image);

        Assert.Equal(4, bitmap.Width);
        Assert.Equal(11, bitmap.Height);
    }

    [Fact]
    public void Render_WideImageRemainsWiderThanBadgeDisplay()
    {
        using var image = EncodeImage(
            width: 100,
            height: 20,
            ImageContainer.Png,
            (_, _) => TestPixel.White);

        var bitmap = _renderer.Render(image);

        Assert.Equal(55, bitmap.Width);
        Assert.True(bitmap.Width > 44);
    }

    [Fact]
    public void Render_RejectsUnsupportedDecoderFormat()
    {
        using var image = EncodeImage(
            width: 11,
            height: 11,
            ImageContainer.Gif,
            (_, _) => TestPixel.White);

        var exception = Assert.Throws<NotSupportedException>(() => _renderer.Render(image));

        Assert.Contains("PNG, JPEG, and BMP", exception.Message);
    }

    [Fact]
    public void Render_PathOverloadReleasesImageFile()
    {
        using var image = EncodeImage(
            width: 11,
            height: 11,
            ImageContainer.Png,
            (_, _) => TestPixel.White);
        var path = Path.Combine(Path.GetTempPath(), $"badgey-{Guid.NewGuid():N}.png");

        try
        {
            File.WriteAllBytes(path, image.ToArray());

            var bitmap = _renderer.Render(path);

            Assert.Equal(11, bitmap.Height);
            File.Delete(path);
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Viewport_CentersNarrowBitmapWithoutChangingSource()
    {
        var source = new MonochromeBitmap(width: 4, height: 11)
        {
            [0, 0] = true,
            [3, 10] = true,
        };

        var viewport = MonochromeBitmapViewport.Create(source, width: 44, centerNarrow: true);

        Assert.Equal(44, viewport.Width);
        Assert.Equal(11, viewport.Height);
        Assert.True(viewport[20, 0]);
        Assert.True(viewport[23, 10]);
        Assert.Equal(2, CountLitPixels(viewport));
        Assert.Equal(4, source.Width);
    }

    [Fact]
    public void Viewport_CropsWideBitmapToFirstColumns()
    {
        var source = new MonochromeBitmap(width: 45, height: 11)
        {
            [0, 0] = true,
            [43, 1] = true,
            [44, 2] = true,
        };

        var viewport = MonochromeBitmapViewport.Create(source, width: 44, centerNarrow: true);

        Assert.True(viewport[0, 0]);
        Assert.True(viewport[43, 1]);
        Assert.Equal(2, CountLitPixels(viewport));
    }

    [Fact]
    public void Render_PacksKnownImageDeterministically()
    {
        byte[] expectedRows =
        [
            0x80,
            0x40,
            0x20,
            0x10,
            0x08,
            0x04,
            0x02,
            0x01,
            0xAA,
            0x55,
            0xFF,
        ];
        using var firstImage = EncodePattern(expectedRows);
        using var secondImage = EncodePattern(expectedRows);

        var first = BadgeBitmapPacker.Pack(_renderer.Render(firstImage));
        var second = BadgeBitmapPacker.Pack(_renderer.Render(secondImage));

        Assert.Equal(expectedRows, first);
        Assert.Equal(first, second);
    }

    private static MemoryStream EncodePattern(IReadOnlyList<byte> rows) =>
        EncodeImage(
            width: 8,
            height: 11,
            ImageContainer.Png,
            (x, y) => (rows[y] & (1 << (7 - x))) != 0
                ? TestPixel.White
                : TestPixel.Black);

    private static MemoryStream EncodeImage(
        int width,
        int height,
        ImageContainer container,
        Func<int, int, TestPixel> getPixel)
    {
        const int bytesPerPixel = 4;
        var stride = checked(width * bytesPerPixel);
        var pixels = new byte[checked(stride * height)];

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var pixel = getPixel(x, y);
                var offset = (y * stride) + (x * bytesPerPixel);
                pixels[offset] = pixel.Blue;
                pixels[offset + 1] = pixel.Green;
                pixels[offset + 2] = pixel.Red;
                pixels[offset + 3] = pixel.Alpha;
            }
        }

        var source = BitmapSource.Create(
            width,
            height,
            dpiX: 96,
            dpiY: 96,
            PixelFormats.Bgra32,
            palette: null,
            pixels,
            stride);
        BitmapEncoder encoder = container switch
        {
            ImageContainer.Png => new PngBitmapEncoder(),
            ImageContainer.Jpeg => new JpegBitmapEncoder { QualityLevel = 100 },
            ImageContainer.Bmp => new BmpBitmapEncoder(),
            ImageContainer.Gif => new GifBitmapEncoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(container)),
        };
        var stream = new MemoryStream();

        encoder.Frames.Add(BitmapFrame.Create(source));
        encoder.Save(stream);
        stream.Position = 0;
        return stream;
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

    public enum ImageContainer
    {
        Png,
        Jpeg,
        Bmp,
        Gif,
    }

    private readonly record struct TestPixel(byte Red, byte Green, byte Blue, byte Alpha)
    {
        public static TestPixel Black { get; } = new(0, 0, 0, 255);

        public static TestPixel White { get; } = new(255, 255, 255, 255);

        public static TestPixel TransparentWhite { get; } = new(255, 255, 255, 0);

        public static TestPixel Gray(byte value) => new(value, value, value, 255);
    }
}
