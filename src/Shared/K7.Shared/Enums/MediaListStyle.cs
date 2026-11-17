namespace K7.Shared.Enums;

/// <summary>
/// How the lists of a media page (seasons, cast, similar titles) show their cards.
/// Stored in preferences: only add values at the end.
/// </summary>
public enum MediaListStyle
{
    /// <summary>One line that scrolls sideways. The default.</summary>
    Carousel,

    /// <summary>Every card at once, on as many lines as it takes.</summary>
    Grid
}
