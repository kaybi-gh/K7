namespace K7.Shared.Enums;

/// <summary>
/// How the hero of a media page (movie, series, episode, person: the backdrop with the title on it) ends on screens wide
/// enough for it. Phones and TVs keep their own. Stored in preferences: only add values at the end.
/// </summary>
public enum MediaHeroStyle
{
    /// <summary>Fades into the page color. The default.</summary>
    Fade,

    /// <summary>Stops on a clean edge, the sections start right under it.</summary>
    Cut,

    /// <summary>In a rounded banner with margins, like the banner of the home page.</summary>
    Banner
}
