namespace Badgey.Protocol;

/// <summary>
/// Display modes verified on the Badgey 44 x 11 badge.
/// </summary>
public enum BadgeDisplayMode : byte
{
    ScrollLeft = 0,
    StaticCentered = 4,
    Animation = 5,
}
