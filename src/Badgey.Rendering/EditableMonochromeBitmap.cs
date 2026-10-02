namespace Badgey.Rendering;

/// <summary>
/// A reusable, transport-independent working copy for editing one physical
/// 44 x 11 badge frame.
/// </summary>
public sealed class EditableMonochromeBitmap
{
    public const int CanvasWidth = 44;
    public const int CanvasHeight = BadgeBitmapPacker.RequiredHeight;

    private readonly MonochromeBitmap _bitmap;

    public EditableMonochromeBitmap()
        : this(new MonochromeBitmap(CanvasWidth, CanvasHeight))
    {
    }

    public EditableMonochromeBitmap(MonochromeBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Width != CanvasWidth || source.Height != CanvasHeight)
        {
            throw new ArgumentException(
                $"An editable bitmap must be {CanvasWidth} x {CanvasHeight} pixels.",
                nameof(source));
        }

        _bitmap = Copy(source);
    }

    public event EventHandler? Changed;

    public int Width => CanvasWidth;

    public int Height => CanvasHeight;

    public bool this[int x, int y] => _bitmap[x, y];

    /// <summary>
    /// Creates an editor canvas from the same centred/first-44-column viewport
    /// used by the static Graphic preview.
    /// </summary>
    public static EditableMonochromeBitmap FromGraphicViewport(
        MonochromeBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Height != CanvasHeight)
        {
            throw new ArgumentException(
                $"A graphic must be {CanvasHeight} pixels high.",
                nameof(source));
        }

        return new EditableMonochromeBitmap(
            MonochromeBitmapViewport.Create(
                source,
                CanvasWidth,
                centerNarrow: true));
    }

    public void SetPixel(int x, int y) => SetPixel(x, y, isLit: true);

    public void ClearPixel(int x, int y) => SetPixel(x, y, isLit: false);

    public void SetPixel(int x, int y, bool isLit)
    {
        if (_bitmap[x, y] == isLit)
        {
            return;
        }

        _bitmap[x, y] = isLit;
        OnChanged();
    }

    public void TogglePixel(int x, int y)
    {
        _bitmap[x, y] = !_bitmap[x, y];
        OnChanged();
    }

    public void ClearAll() => SetAll(isLit: false);

    public void FillAll() => SetAll(isLit: true);

    public void Invert()
    {
        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                _bitmap[x, y] = !_bitmap[x, y];
            }
        }

        OnChanged();
    }

    /// <summary>
    /// Paints every cell crossed by a pointer sample pair. Keeping this in the
    /// model prevents fast mouse movement from leaving gaps between cells.
    /// </summary>
    public void PaintLine(
        int startX,
        int startY,
        int endX,
        int endY,
        bool isLit)
    {
        ValidateCoordinates(startX, startY);
        ValidateCoordinates(endX, endY);

        var x = startX;
        var y = startY;
        var deltaX = Math.Abs(endX - startX);
        var stepX = startX < endX ? 1 : -1;
        var deltaY = -Math.Abs(endY - startY);
        var stepY = startY < endY ? 1 : -1;
        var error = deltaX + deltaY;
        var changed = false;

        while (true)
        {
            if (_bitmap[x, y] != isLit)
            {
                _bitmap[x, y] = isLit;
                changed = true;
            }

            if (x == endX && y == endY)
            {
                break;
            }

            var doubledError = error * 2;

            if (doubledError >= deltaY)
            {
                error += deltaY;
                x += stepX;
            }

            if (doubledError <= deltaX)
            {
                error += deltaX;
                y += stepY;
            }
        }

        if (changed)
        {
            OnChanged();
        }
    }

    /// <summary>
    /// Returns a deep copy suitable for preview, packing, or accepting an edit.
    /// </summary>
    public MonochromeBitmap CreateBitmap() => Copy(_bitmap);

    public EditableMonochromeBitmap Clone() => new(CreateBitmap());

    private void SetAll(bool isLit)
    {
        var changed = false;

        for (var y = 0; y < Height; y++)
        {
            for (var x = 0; x < Width; x++)
            {
                if (_bitmap[x, y] == isLit)
                {
                    continue;
                }

                _bitmap[x, y] = isLit;
                changed = true;
            }
        }

        if (changed)
        {
            OnChanged();
        }
    }

    private void ValidateCoordinates(int x, int y)
    {
        _ = _bitmap[x, y];
    }

    private static MonochromeBitmap Copy(MonochromeBitmap source)
    {
        var copy = new MonochromeBitmap(source.Width, source.Height);

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                copy[x, y] = source[x, y];
            }
        }

        return copy;
    }

    private void OnChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
