using Badgey.Hardware;
using Badgey.Protocol;
using Badgey.Rendering;

namespace Badgey;

/// <summary>
/// Composes Badgey's rendering, protocol, and hardware layers for the desktop app.
/// </summary>
public sealed class BadgeController
{
    private readonly WindowsTextRenderer _renderer;

    public BadgeController(WindowsTextRenderer renderer)
    {
        _renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
    }

    public MonochromeBitmap Render(string message) => _renderer.Render(message);

    public Task SendAsync(MonochromeBitmap bitmap, BadgeDisplayMode mode, int speed)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var packedBitmap = BadgeBitmapPacker.Pack(bitmap);
        var upload = BadgeProtocol.CreateUpload(packedBitmap, mode, speed);

        return Task.Run(() =>
        {
            using var transport = new BadgeHidTransport();
            transport.Open();
            transport.Write(upload);
        });
    }
}
