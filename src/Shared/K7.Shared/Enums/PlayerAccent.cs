namespace K7.Shared.Enums;

/// <summary>
/// Color of the active parts of a player (progress, main button). Stored in preferences: only
/// add values at the end.
/// </summary>
public enum PlayerAccent
{
    /// <summary>The usual color of the player: the color of the cover for music, red for video.</summary>
    Auto,

    /// <summary>The accent color of the theme.</summary>
    Theme
}
