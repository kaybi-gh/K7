namespace K7.Shared.Enums;

/// <summary>
/// Layout of the full screen music player on wide screens. Phones keep their single column.
/// Stored in preferences: only add values at the end.
/// </summary>
public enum AudioPlayerLayout
{
    /// <summary>The cover next to the controls. The default.</summary>
    Side,

    /// <summary>A large centered cover, the controls under it.</summary>
    Centered
}
