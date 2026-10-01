namespace Badgey.Rendering;

/// <summary>
/// Lossless transformations for logical monochrome bitmaps.
/// </summary>
public static class MonochromeBitmapTransforms
{
    /// <summary>
    /// Creates a bitmap in which every logical pixel is the inverse of the source.
    /// </summary>
    public static MonochromeBitmap Invert(MonochromeBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);

        var result = new MonochromeBitmap(source.Width, source.Height);

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                result[x, y] = !source[x, y];
            }
        }

        return result;
    }
}
