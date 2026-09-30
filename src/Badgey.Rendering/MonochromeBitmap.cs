namespace Badgey.Rendering;

/// <summary>
/// A transport-independent, row-major canvas of logical on/off pixels.
/// </summary>
public sealed class MonochromeBitmap
{
    private readonly bool[] _pixels;

    public MonochromeBitmap(int width, int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(width);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);

        Width = width;
        Height = height;
        _pixels = new bool[checked(width * height)];
    }

    public int Width { get; }

    public int Height { get; }

    public bool this[int x, int y]
    {
        get
        {
            ValidateCoordinates(x, y);
            return _pixels[(y * Width) + x];
        }
        set
        {
            ValidateCoordinates(x, y);
            _pixels[(y * Width) + x] = value;
        }
    }

    private void ValidateCoordinates(int x, int y)
    {
        if ((uint)x >= (uint)Width)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if ((uint)y >= (uint)Height)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }
    }
}
