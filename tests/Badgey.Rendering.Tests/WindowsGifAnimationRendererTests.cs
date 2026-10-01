using System.IO;
using Badgey.Rendering;

namespace Badgey.Rendering.Tests;

public class WindowsGifAnimationRendererTests
{
    private readonly WindowsGifAnimationRenderer _renderer = new();

    [Fact]
    public void Render_ExtractsAllFramesInDeterministicOrder()
    {
        using var gif = CreateGif(
            width: 44,
            height: 11,
            FullFrameWithLitPixel(44, 11, x: 1, y: 1),
            FullFrameWithLitPixel(44, 11, x: 2, y: 1),
            FullFrameWithLitPixel(44, 11, x: 3, y: 1));

        var animation = _renderer.Render(gif);

        Assert.Equal(3, animation.FrameCount);

        for (var frameIndex = 0; frameIndex < animation.FrameCount; frameIndex++)
        {
            Assert.Equal(44, animation[frameIndex].Width);
            Assert.Equal(11, animation[frameIndex].Height);
            Assert.True(animation[frameIndex][frameIndex + 1, 1]);
            Assert.Equal(1, CountLitPixels(animation[frameIndex]));
        }
    }

    [Fact]
    public void Render_TransparentPixelsPreserveTheComposedFrameBeneathThem()
    {
        var transparentOverlay = Enumerable.Repeat((byte)0, 44 * 11).ToArray();
        transparentOverlay[(1 * 44) + 2] = 1;
        using var gif = CreateGif(
            width: 44,
            height: 11,
            FullFrameWithLitPixel(44, 11, x: 1, y: 1),
            new GifFrame(0, 0, 44, 11, transparentOverlay));

        var animation = _renderer.Render(gif);

        Assert.True(animation[1][1, 1]);
        Assert.True(animation[1][2, 1]);
        Assert.Equal(2, CountLitPixels(animation[1]));
    }

    [Fact]
    public void Render_TransparentInitialCanvasRemainsUnlit()
    {
        var pixels = Enumerable.Repeat((byte)0, 44 * 11).ToArray();
        pixels[(5 * 44) + 6] = 1;
        using var gif = CreateGif(
            width: 44,
            height: 11,
            new GifFrame(0, 0, 44, 11, pixels));

        var animation = _renderer.Render(gif);

        Assert.True(animation[0][6, 5]);
        Assert.Equal(1, CountLitPixels(animation[0]));
    }

    [Fact]
    public void Render_RestoreBackgroundClearsThePreviousFrameRectangle()
    {
        using var gif = CreateGif(
            width: 44,
            height: 11,
            new GifFrame(1, 1, 1, 1, [1], Disposal: 2),
            new GifFrame(2, 1, 1, 1, [1]));

        var animation = _renderer.Render(gif);

        Assert.True(animation[0][1, 1]);
        Assert.False(animation[1][1, 1]);
        Assert.True(animation[1][2, 1]);
        Assert.Equal(1, CountLitPixels(animation[1]));
    }

    [Fact]
    public void Render_RestorePreviousRestoresThePreFrameCanvas()
    {
        using var gif = CreateGif(
            width: 44,
            height: 11,
            FullFrameWithLitPixel(44, 11, x: 0, y: 1),
            new GifFrame(1, 1, 1, 1, [1], Disposal: 3),
            new GifFrame(2, 1, 1, 1, [1]));

        var animation = _renderer.Render(gif);

        Assert.True(animation[1][0, 1]);
        Assert.True(animation[1][1, 1]);
        Assert.Equal(2, CountLitPixels(animation[1]));

        Assert.True(animation[2][0, 1]);
        Assert.False(animation[2][1, 1]);
        Assert.True(animation[2][2, 1]);
        Assert.Equal(2, CountLitPixels(animation[2]));
    }

    [Fact]
    public void Render_TallAnimationIsAspectFitAndCenteredHorizontally()
    {
        using var gif = CreateGif(
            width: 2,
            height: 4,
            SolidFrame(width: 2, height: 4, paletteIndex: 1));

        var frame = _renderer.Render(gif)[0];

        AssertRectangle(frame, left: 19, top: 0, width: 6, height: 11);
    }

    [Fact]
    public void Render_WideAnimationIsAspectFitAndCenteredVertically()
    {
        using var gif = CreateGif(
            width: 8,
            height: 1,
            SolidFrame(width: 8, height: 1, paletteIndex: 1));

        var frame = _renderer.Render(gif)[0];

        AssertRectangle(frame, left: 0, top: 2, width: 44, height: 6);
    }

    [Fact]
    public void Render_PathOverloadReleasesGifFile()
    {
        using var gif = CreateGif(
            width: 44,
            height: 11,
            FullFrameWithLitPixel(44, 11, x: 1, y: 1));
        var path = Path.Combine(Path.GetTempPath(), $"badgey-{Guid.NewGuid():N}.gif");

        try
        {
            File.WriteAllBytes(path, gif.ToArray());

            var animation = _renderer.Render(path);

            Assert.Single(animation.Frames);
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
    public void Animation_InversionReversesEveryPixelInEveryFrame()
    {
        var source = CreateAnimation();

        var inverted = MonochromeAnimationTransforms.Invert(source);

        Assert.Equal(source.FrameCount, inverted.FrameCount);

        for (var frameIndex = 0; frameIndex < source.FrameCount; frameIndex++)
        {
            for (var y = 0; y < MonochromeAnimation.FrameHeight; y++)
            {
                for (var x = 0; x < MonochromeAnimation.FrameWidth; x++)
                {
                    Assert.NotEqual(
                        source[frameIndex][x, y],
                        inverted[frameIndex][x, y]);
                }
            }
        }
    }

    [Fact]
    public void Animation_DoubleInversionRestoresEveryFrame()
    {
        var source = CreateAnimation();

        var restored = MonochromeAnimationTransforms.Invert(
            MonochromeAnimationTransforms.Invert(source));

        AssertAnimationsEqual(source, restored);
    }

    [Fact]
    public void AnimationState_NewLoadResetsInversionAndRetainsNewSource()
    {
        var first = CreateAnimation();
        var second = new MonochromeAnimation([CreateBadgeFrame(x: 3, y: 4)]);
        var state = new AnimationBitmapState();

        state.Load(first);
        state.SetInverted(true);
        state.Load(second);

        Assert.False(state.IsInverted);
        Assert.Same(second, state.Source);
        Assert.Same(second, state.Current);
    }

    [Fact]
    public void AnimationState_RepeatedToggleAlwaysDerivesFromOriginalSource()
    {
        var source = CreateAnimation();
        var state = new AnimationBitmapState();
        state.Load(source);

        state.SetInverted(true);
        var firstInversion = state.Current!;
        state.SetInverted(false);
        state.SetInverted(true);

        Assert.True(state.IsInverted);
        Assert.NotSame(firstInversion, state.Current);
        AssertAnimationsEqual(firstInversion, state.Current!);
        Assert.Same(source, state.Source);
    }

    [Fact]
    public void AnimationModel_ValidatesFrames()
    {
        Assert.Throws<ArgumentNullException>(() => new MonochromeAnimation(null!));
        Assert.Throws<ArgumentException>(() => new MonochromeAnimation([]));
        Assert.Throws<ArgumentException>(
            () => new MonochromeAnimation([new MonochromeBitmap(43, 11)]));
        Assert.Throws<ArgumentException>(
            () => new MonochromeAnimation([new MonochromeBitmap(44, 10)]));
    }

    private static MonochromeAnimation CreateAnimation() =>
        new(
        [
            CreateBadgeFrame(x: 0, y: 0),
            CreateBadgeFrame(x: 43, y: 10),
        ]);

    private static MonochromeBitmap CreateBadgeFrame(int x, int y) =>
        new(MonochromeAnimation.FrameWidth, MonochromeAnimation.FrameHeight)
        {
            [x, y] = true,
        };

    private static GifFrame FullFrameWithLitPixel(
        int width,
        int height,
        int x,
        int y)
    {
        var pixels = Enumerable.Repeat((byte)2, checked(width * height)).ToArray();
        pixels[(y * width) + x] = 1;
        return new GifFrame(0, 0, width, height, pixels);
    }

    private static GifFrame SolidFrame(int width, int height, byte paletteIndex) =>
        new(
            0,
            0,
            width,
            height,
            Enumerable.Repeat(paletteIndex, checked(width * height)).ToArray());

    private static MemoryStream CreateGif(
        int width,
        int height,
        params GifFrame[] frames)
    {
        var stream = new MemoryStream();

        using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            writer.Write("GIF89a"u8);
            writer.Write(checked((ushort)width));
            writer.Write(checked((ushort)height));
            writer.Write((byte)0xF1); // Global colour table, 8-bit resolution, 4 entries.
            writer.Write((byte)0); // Background colour index.
            writer.Write((byte)0); // Square pixels.

            // Index 0 is transparent white; 1 is opaque white; 2 is opaque black;
            // index 3 is unused. GIF transparency is supplied by each frame's GCE.
            writer.Write(new byte[]
            {
                255, 255, 255,
                255, 255, 255,
                0, 0, 0,
                127, 127, 127,
            });

            foreach (var frame in frames)
            {
                if (frame.Pixels.Length != checked(frame.Width * frame.Height))
                {
                    throw new ArgumentException("GIF test frame has an invalid pixel count.");
                }

                writer.Write(new byte[] { 0x21, 0xF9, 0x04 });
                writer.Write((byte)((frame.Disposal << 2) | 0x01));
                writer.Write((ushort)5);
                writer.Write((byte)0);
                writer.Write((byte)0);

                writer.Write((byte)0x2C);
                writer.Write(checked((ushort)frame.Left));
                writer.Write(checked((ushort)frame.Top));
                writer.Write(checked((ushort)frame.Width));
                writer.Write(checked((ushort)frame.Height));
                writer.Write((byte)0);

                writer.Write((byte)2);
                var compressed = EncodeGifImageData(frame.Pixels);

                for (var offset = 0; offset < compressed.Length; offset += 255)
                {
                    var blockLength = Math.Min(255, compressed.Length - offset);
                    writer.Write((byte)blockLength);
                    writer.Write(compressed, offset, blockLength);
                }

                writer.Write((byte)0);
            }

            writer.Write((byte)0x3B);
        }

        stream.Position = 0;
        return stream;
    }

    private static byte[] EncodeGifImageData(IReadOnlyList<byte> paletteIndices)
    {
        const int clearCode = 4;
        const int endCode = 5;
        const int codeWidth = 3;
        var codes = new List<int>(checked((paletteIndices.Count * 2) + 1));

        foreach (var paletteIndex in paletteIndices)
        {
            codes.Add(clearCode);
            codes.Add(paletteIndex);
        }

        codes.Add(endCode);

        var bytes = new List<byte>();
        var bitBuffer = 0;
        var bufferedBitCount = 0;

        foreach (var code in codes)
        {
            bitBuffer |= code << bufferedBitCount;
            bufferedBitCount += codeWidth;

            while (bufferedBitCount >= 8)
            {
                bytes.Add((byte)bitBuffer);
                bitBuffer >>= 8;
                bufferedBitCount -= 8;
            }
        }

        if (bufferedBitCount > 0)
        {
            bytes.Add((byte)bitBuffer);
        }

        return bytes.ToArray();
    }

    private static void AssertRectangle(
        MonochromeBitmap bitmap,
        int left,
        int top,
        int width,
        int height)
    {
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var expected = x >= left
                    && x < left + width
                    && y >= top
                    && y < top + height;
                Assert.Equal(expected, bitmap[x, y]);
            }
        }
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

    private static void AssertAnimationsEqual(
        MonochromeAnimation expected,
        MonochromeAnimation actual)
    {
        Assert.Equal(expected.FrameCount, actual.FrameCount);

        for (var frameIndex = 0; frameIndex < expected.FrameCount; frameIndex++)
        {
            for (var y = 0; y < MonochromeAnimation.FrameHeight; y++)
            {
                for (var x = 0; x < MonochromeAnimation.FrameWidth; x++)
                {
                    Assert.Equal(
                        expected[frameIndex][x, y],
                        actual[frameIndex][x, y]);
                }
            }
        }
    }

    private sealed record GifFrame(
        int Left,
        int Top,
        int Width,
        int Height,
        byte[] Pixels,
        byte Disposal = 1);
}
