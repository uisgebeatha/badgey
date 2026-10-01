using System.Collections.ObjectModel;

namespace Badgey.Rendering;

/// <summary>
/// An ordered, transport-independent sequence of badge-sized monochrome frames.
/// </summary>
public sealed class MonochromeAnimation
{
    public const int FrameWidth = 44;
    public const int FrameHeight = BadgeBitmapPacker.RequiredHeight;

    private readonly ReadOnlyCollection<MonochromeBitmap> _frames;

    public MonochromeAnimation(IEnumerable<MonochromeBitmap> frames)
    {
        ArgumentNullException.ThrowIfNull(frames);

        var materialized = frames.ToArray();

        if (materialized.Length == 0)
        {
            throw new ArgumentException(
                "An animation must contain at least one frame.",
                nameof(frames));
        }

        for (var index = 0; index < materialized.Length; index++)
        {
            var frame = materialized[index]
                ?? throw new ArgumentException(
                    $"Animation frame {index} cannot be null.",
                    nameof(frames));

            if (frame.Width != FrameWidth || frame.Height != FrameHeight)
            {
                throw new ArgumentException(
                    $"Animation frame {index} must be exactly {FrameWidth}x{FrameHeight} pixels.",
                    nameof(frames));
            }
        }

        _frames = Array.AsReadOnly(materialized);
    }

    public IReadOnlyList<MonochromeBitmap> Frames => _frames;

    public int FrameCount => _frames.Count;

    public MonochromeBitmap this[int index] => _frames[index];
}
