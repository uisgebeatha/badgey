namespace Badgey.Rendering;

/// <summary>
/// Maps columns in a repeating bitmap-and-gap sequence.
/// </summary>
public static class MonochromeBitmapLoop
{
    public static int GetCycleWidth(int sourceWidth, int gapColumns)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sourceWidth, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(gapColumns);

        return checked(sourceWidth + gapColumns);
    }

    /// <summary>
    /// Returns the source column for a column in the repeating sequence, or -1
    /// when the requested column falls within the unlit gap.
    /// </summary>
    public static int GetSourceColumn(
        int sourceWidth,
        int gapColumns,
        int loopColumn)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(loopColumn);

        var cycleColumn = loopColumn % GetCycleWidth(sourceWidth, gapColumns);
        return cycleColumn < sourceWidth ? cycleColumn : -1;
    }
}
