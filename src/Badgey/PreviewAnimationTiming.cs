namespace Badgey;

/// <summary>
/// Preview-only frame timing based on the reference badge's global animation
/// speed. This is intentionally separate from scroll preview timing and from
/// protocol speed encoding.
/// </summary>
internal static class PreviewAnimationTiming
{
    private static readonly int[] MillisecondsPerFrame =
    [
        833,
        769,
        500,
        417,
        357,
        222,
        133,
        67,
    ];

    public static TimeSpan GetInterval(int speed)
    {
        if (speed is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(speed),
                speed,
                "Animation preview speed must be from 1 to 8.");
        }

        return TimeSpan.FromMilliseconds(MillisecondsPerFrame[speed - 1]);
    }
}
