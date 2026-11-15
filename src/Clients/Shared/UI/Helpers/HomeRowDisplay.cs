using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// What a home row shows for its display type: which items, and which card shape.
/// Shared by the home page and the layout preview so both always agree.
/// </summary>
public static class HomeRowDisplay
{
    /// <summary>Titles a banner switches between when the row does not say.</summary>
    public const int DefaultBannerCount = 5;

    /// <summary>Most titles a banner switches between: beyond that the dots stop being usable.</summary>
    public const int MaxBannerCount = 15;

    /// <summary>The items of the row, without duplicates.</summary>
    public static List<MediaCardViewModel> Items(IEnumerable<MediaCardViewModel> items) =>
        items.DistinctBy(i => i.Id).ToList();

    /// <summary>
    /// The titles a banner row features: the first ones with a backdrop, as many as the row asks for. Music is
    /// left out, a square cover does not fill a banner. Empty when the row is no banner or has
    /// nothing to feature: the row then shows as posters.
    /// </summary>
    public static List<MediaCardViewModel> Featured(
        HomeRowDisplayType displayType,
        IEnumerable<MediaCardViewModel> items,
        int count = DefaultBannerCount) =>
        displayType.IsBanner()
            ? items.Where(IsFeaturable).DistinctBy(i => i.Id).Take(Math.Clamp(count, 1, MaxBannerCount)).ToList()
            : [];

    /// <summary>
    /// Card shape of an item. A landscape row keeps every card wide, also the ones without a
    /// backdrop, so the row stays even.
    /// </summary>
    public static MediaCardVariant Variant(HomeRowDisplayType displayType, MediaCardViewModel item)
    {
        if (displayType == HomeRowDisplayType.Landscape)
            return MediaCardVariant.Backdrop;

        return item.Kind == MediaCardKind.Cover ? MediaCardVariant.Cover : MediaCardVariant.Poster;
    }

    private static bool IsFeaturable(MediaCardViewModel item) =>
        !string.IsNullOrEmpty(item.BackdropUrl)
        && item.MediaType is not (MediaType.MusicAlbum or MediaType.MusicTrack or MediaType.MusicArtist);
}
