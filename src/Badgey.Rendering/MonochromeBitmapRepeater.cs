namespace Badgey.Rendering;

/// <summary>
/// Builds a horizontal sequence of complete monochrome bitmap copies.
/// </summary>
public static class MonochromeBitmapRepeater
{
    /// <summary>
    /// Repeats a bitmap horizontally, inserting separator columns between copies.
    /// No gap is appended after the final copy.
    /// </summary>
    public static MonochromeBitmap RepeatHorizontally(
        MonochromeBitmap source,
        int repetitions,
        int gapColumns,
        bool gapValue = false)
    {
        ValidateSource(source);
        ArgumentOutOfRangeException.ThrowIfLessThan(repetitions, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(gapColumns);

        var repeatedWidth = checked(
            (source.Width * repetitions)
            + (gapColumns * (repetitions - 1)));
        var result = new MonochromeBitmap(repeatedWidth, source.Height);

        for (var repetition = 0; repetition < repetitions; repetition++)
        {
            var destinationLeft = checked((int)(
                repetition * ((long)source.Width + gapColumns)));

            for (var y = 0; y < source.Height; y++)
            {
                for (var x = 0; x < source.Width; x++)
                {
                    result[destinationLeft + x, y] = source[x, y];
                }

                if (gapValue && repetition < repetitions - 1)
                {
                    for (var gapX = 0; gapX < gapColumns; gapX++)
                    {
                        result[destinationLeft + source.Width + gapX, y] = true;
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Repeats as many complete copies as fit within <paramref name="maximumWidth"/>.
    /// The source is never cropped or partially repeated.
    /// </summary>
    public static MonochromeBitmap RepeatToMaximumWidth(
        MonochromeBitmap source,
        int gapColumns,
        int maximumWidth,
        bool gapValue = false)
    {
        ValidateSource(source);
        ArgumentOutOfRangeException.ThrowIfNegative(gapColumns);

        if (maximumWidth < source.Width)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumWidth),
                maximumWidth,
                "Maximum width must fit at least one complete source bitmap.");
        }

        var repetitions = checked((int)(((long)maximumWidth + gapColumns)
            / ((long)source.Width + gapColumns)));

        return RepeatHorizontally(source, repetitions, gapColumns, gapValue);
    }

    private static void ValidateSource(MonochromeBitmap source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (source.Width == 0)
        {
            throw new ArgumentException(
                "The source bitmap must contain at least one column.",
                nameof(source));
        }
    }
}
