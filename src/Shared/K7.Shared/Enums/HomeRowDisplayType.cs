namespace K7.Shared.Enums;

/// <summary>
/// How a home row shows its items. Two families: a carousel of cards (<see cref="Carousel"/>,
/// <see cref="Landscape"/>) or a banner that features one title at a time
/// (<see cref="Featured"/>, <see cref="FeaturedBleed"/>). Stored in saved layouts: only add
/// values at the end.
/// </summary>
public enum HomeRowDisplayType
{
    /// <summary>Carousel of posters and covers, the default.</summary>
    Carousel,

    /// <summary>Carousel of wide 16:9 cards that use the backdrop of each item.</summary>
    Landscape,

    /// <summary>Banner kept inside the page margins, with rounded corners.</summary>
    Featured,

    /// <summary>Banner that runs edge to edge and fades into the page.</summary>
    FeaturedBleed
}

public static class HomeRowDisplayTypeExtensions
{
    /// <summary>
    /// True for the banner family. Banners show as posters on TV, which has its own hero, and
    /// need the detailed feed for their overview.
    /// </summary>
    public static bool IsBanner(this HomeRowDisplayType displayType) =>
        displayType is HomeRowDisplayType.Featured or HomeRowDisplayType.FeaturedBleed;
}
