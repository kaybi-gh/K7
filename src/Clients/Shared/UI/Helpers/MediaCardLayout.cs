using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// Poster / cover / still geometry for browse grids and their skeletons.
/// </summary>
public static class MediaCardLayout
{
    public const int BrowsePosterWidth = 160;

    public static MediaCardVariant VariantForBrowseMediaType(MediaType mediaType) => mediaType switch
    {
        MediaType.MusicAlbum or MediaType.MusicTrack or MediaType.MusicArtist => MediaCardVariant.Cover,
        MediaType.SerieEpisode => MediaCardVariant.Backdrop,
        _ => MediaCardVariant.Poster
    };

    public static float GridAspectRatio(MediaCardVariant variant) => variant switch
    {
        MediaCardVariant.Cover => 1f,
        MediaCardVariant.Backdrop => 9f / 16f,
        _ => 1.5f
    };

    /// <summary>
    /// Tile width so a still matches poster height (16:9 vs 2:3), not poster width.
    /// </summary>
    public static int GridItemWidth(MediaCardVariant variant, int posterWidth = BrowsePosterWidth)
    {
        if (variant is not MediaCardVariant.Backdrop)
            return posterWidth;

        var posterHeight = posterWidth * GridAspectRatio(MediaCardVariant.Poster);
        return (int)MathF.Round(posterHeight / GridAspectRatio(MediaCardVariant.Backdrop));
    }

    /// <summary>CSS <c>aspect-ratio</c> list (width / height) for placeholders.</summary>
    public static string CssRatio(MediaCardVariant variant) => variant switch
    {
        MediaCardVariant.Cover => "1 / 1",
        MediaCardVariant.Backdrop => "16 / 9",
        _ => "2 / 3"
    };
}
