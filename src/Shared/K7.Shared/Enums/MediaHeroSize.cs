namespace K7.Shared.Enums;

/// <summary>
/// How much of the screen the hero of a media page takes: the backdrop with the title on it.
/// Stored in preferences: only add values at the end.
/// </summary>
public enum MediaHeroSize
{
    /// <summary>The whole screen: the sections come when scrolling (immersive). The default.</summary>
    Full,

    /// <summary>About half of the screen, the first sections show under it.</summary>
    Half,

    /// <summary>About a third of the screen.</summary>
    Third
}
