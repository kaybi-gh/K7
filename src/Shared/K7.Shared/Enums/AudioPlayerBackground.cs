namespace K7.Shared.Enums;

/// <summary>
/// Background of the full screen music player. Stored in preferences: only add values at the end.
/// </summary>
public enum AudioPlayerBackground
{
    /// <summary>The cover, blurred and darkened. The default.</summary>
    Blur,

    /// <summary>A gradient of the main color of the cover.</summary>
    Gradient,

    /// <summary>A plain dark background.</summary>
    Plain
}
