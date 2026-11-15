namespace K7.Shared.Enums;

/// <summary>
/// Where the mini music player sits on screens wide enough for a top navigation bar (desktop,
/// tablet, TV). On a phone it always floats above the bottom bar. Stored in preferences: only
/// add values at the end.
/// </summary>
public enum MiniPlayerPlacement
{
    /// <summary>Inside the navigation bar, the default.</summary>
    Auto,

    /// <summary>In a band of its own at the bottom of the screen.</summary>
    Bottom
}
