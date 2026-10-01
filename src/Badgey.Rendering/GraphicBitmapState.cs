namespace Badgey.Rendering;

/// <summary>
/// Retains an original rendered graphic and derives its current inversion from
/// that source so repeated toggles never accumulate transformations.
/// </summary>
public sealed class GraphicBitmapState
{
    public MonochromeBitmap? Source { get; private set; }

    public MonochromeBitmap? Current { get; private set; }

    public bool IsInverted { get; private set; }

    public void Load(MonochromeBitmap source)
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
                "A source bitmap must be loaded before inversion can be changed.");
        }

        IsInverted = isInverted;
        Current = isInverted
            ? MonochromeBitmapTransforms.Invert(Source)
            : Source;
    }
}
