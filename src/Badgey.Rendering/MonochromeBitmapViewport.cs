namespace Badgey.Rendering;

/// <summary>
/// Creates a fixed-width preview viewport without changing the bitmap used for upload.
/// </summary>
public static class MonochromeBitmapViewport
{
    public static MonochromeBitmap Create(
        MonochromeBitmap source,
        int width,
        bool centerNarrow)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);

        var viewport = new MonochromeBitmap(width, source.Height);
        var copyWidth = Math.Min(source.Width, width);
        var destinationLeft = centerNarrow && source.Width < width
            ? (width - source.Width) / 2
            : 0;

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < copyWidth; x++)
            {
                viewport[destinationLeft + x, y] = source[x, y];
            }
        }

        return viewport;
    }
}
