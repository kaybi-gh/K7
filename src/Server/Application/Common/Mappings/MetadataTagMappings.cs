using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Common.Mappings;

public static class MetadataTagMappings
{
    public static IReadOnlyList<string> GetTagDisplayNames(this BaseMedia media, MetadataTagKind kind) =>
        media.MetadataTags
            .Where(mt => mt.MetadataTag.Kind == kind)
            .Select(mt => mt.MetadataTag.DisplayName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static string? GetTagDisplayName(this BaseMedia media, MetadataTagKind kind) =>
        GetTagDisplayNames(media, kind).FirstOrDefault();

    public static IReadOnlyList<string> GetGenreDisplayNames(this BaseMedia media, bool inherit = true)
    {
        var genres = media.GetTagDisplayNames(MetadataTagKind.Genre);
        if (genres.Count > 0 || !inherit)
            return genres;

        return media switch
        {
            MusicTrack track => track.Album?.GetTagDisplayNames(MetadataTagKind.Genre) ?? [],
            SerieSeason season => season.Serie?.GetTagDisplayNames(MetadataTagKind.Genre) ?? [],
            SerieEpisode episode => episode.Serie?.GetTagDisplayNames(MetadataTagKind.Genre) ?? [],
            MusicArtist artist => artist.Albums
                .SelectMany(a => a.GetTagDisplayNames(MetadataTagKind.Genre))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => []
        };
    }

    public static string? GetPrimaryGenreDisplayName(this BaseMedia media) =>
        GetGenreDisplayNames(media, inherit: true).FirstOrDefault();

    public static IReadOnlyList<string> GetStudioDisplayNames(this BaseMedia media) =>
        media switch
        {
            Movie or Serie => media.GetTagDisplayNames(MetadataTagKind.Studio),
            _ => []
        };

    public static string? GetContentRatingDisplayName(this BaseMedia media) =>
        media switch
        {
            Movie or Serie => media.GetTagDisplayName(MetadataTagKind.ContentRating),
            _ => null
        };

    public static string? GetNetworkDisplayName(this BaseMedia media) =>
        media is Serie ? media.GetTagDisplayName(MetadataTagKind.Network) : null;
}
