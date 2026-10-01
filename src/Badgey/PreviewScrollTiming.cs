namespace Badgey;

/// <summary>
/// Preview-only timing calibrated against the physical badge. Protocol speed
/// encoding remains independently defined by Badgey.Protocol.
/// </summary>
internal static class PreviewScrollTiming
{
    private static readonly int[] MillisecondsPerColumn =
    [
        240,
        195,
        155,
        120,
        82,
        55,
        32,
        19,
    ];

    public static TimeSpan GetInterval(int speed)
    {
        if (speed is < 1 or > 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(speed),
                speed,
                "Preview speed must be from 1 to 8.");
        }

        return TimeSpan.FromMilliseconds(MillisecondsPerColumn[speed - 1]);
    }
}
