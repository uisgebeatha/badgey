namespace Badgey.Rendering;

/// <summary>
/// Retains an original animation and derives its current inversion from that
/// source so repeated toggles never accumulate transformations.
/// </summary>
public sealed class AnimationBitmapState
{
    public MonochromeAnimation? Source { get; private set; }

    public MonochromeAnimation? Current { get; private set; }

    public bool IsInverted { get; private set; }

    public void Load(MonochromeAnimation source)
    {
        ArgumentNullException.ThrowIfNull(source);

        Source = source;
        Current = source;
        IsInverted = false;
    }

    public void SetInverted(bool isInverted)
    {
        if (Source is null)
        {
            throw new InvalidOperationException(
                "A source animation must be loaded before inversion can be changed.");
        }

        IsInverted = isInverted;
        Current = isInverted
            ? MonochromeAnimationTransforms.Invert(Source)
            : Source;
    }
}
