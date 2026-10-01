namespace Badgey.Rendering;

/// <summary>
/// Lossless transformations for monochrome animations.
/// </summary>
public static class MonochromeAnimationTransforms
{
    /// <summary>
    /// Creates an animation in which every logical pixel in every frame is
    /// the inverse of the source animation.
    /// </summary>
    public static MonochromeAnimation Invert(MonochromeAnimation source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new MonochromeAnimation(
            source.Frames.Select(MonochromeBitmapTransforms.Invert));
    }
}
