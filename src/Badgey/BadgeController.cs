using Badgey.Hardware;
using Badgey.Protocol;
using Badgey.Rendering;

namespace Badgey;

/// <summary>
/// Composes Badgey's rendering, protocol, and hardware layers for the desktop app.
/// </summary>
public sealed class BadgeController
{
    private readonly WindowsTextRenderer _textRenderer;
    private readonly WindowsImageRenderer _imageRenderer;
    private readonly WindowsGifAnimationRenderer _animationRenderer;

    public BadgeController(
        WindowsTextRenderer textRenderer,
        WindowsImageRenderer imageRenderer,
        WindowsGifAnimationRenderer animationRenderer)
    {
        _textRenderer = textRenderer ?? throw new ArgumentNullException(nameof(textRenderer));
        _imageRenderer = imageRenderer ?? throw new ArgumentNullException(nameof(imageRenderer));
        _animationRenderer = animationRenderer ?? throw new ArgumentNullException(nameof(animationRenderer));
    }

    public MonochromeBitmap Render(string message) => _textRenderer.Render(message);

    public MonochromeBitmap RenderImage(string path) => _imageRenderer.Render(path);

    public MonochromeAnimation RenderAnimation(string path) => _animationRenderer.Render(path);

    public Task SendAsync(MonochromeBitmap bitmap, BadgeDisplayMode mode, int speed)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var packedBitmap = BadgeBitmapPacker.Pack(bitmap);
        var upload = BadgeProtocol.CreateUpload(packedBitmap, mode, speed);

        return WriteUploadAsync(upload);
    }

    public Task SendAnimationAsync(MonochromeAnimation animation, int speed)
    {
        ArgumentNullException.ThrowIfNull(animation);

        var packedFrames = animation.Frames
            .Select(BadgeBitmapPacker.Pack)
            .ToArray();
        var upload = BadgeProtocol.CreateAnimationUpload(packedFrames, speed);

        return WriteUploadAsync(upload);
    }

    private static Task WriteUploadAsync(byte[] upload) =>
        Task.Run(() =>
        {
            using var transport = new BadgeHidTransport();
            transport.Open();
            transport.Write(upload);
        });
}
