namespace Badgey.Protocol;

/// <summary>
/// Describes the packed content and display settings for one badge message slot.
/// </summary>
public sealed record BadgeSlotUpload(
    byte[] PackedData,
    BadgeDisplayMode Mode,
    int Speed);
