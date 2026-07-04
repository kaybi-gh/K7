using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;

namespace K7.Clients.Shared.Helpers;

public static class BrowseSortUrlMapping
{
    public static string? ToUrlValue(MediaOrderingOption sort) =>
        sort switch
        {
            MediaOrderingOption.TitleAsc => null,
            MediaOrderingOption.TitleDesc => "titleDesc",
            MediaOrderingOption.CreatedDesc => "createdDesc",
            MediaOrderingOption.CreatedAsc => "createdAsc",
            MediaOrderingOption.ReleaseDateDesc => "releaseDateDesc",
            MediaOrderingOption.ReleaseDateAsc => "releaseDateAsc",
            MediaOrderingOption.LocalRatingDesc => "ratingDesc",
            MediaOrderingOption.LocalRatingAsc => "ratingAsc",
            MediaOrderingOption.PlayCountDesc => "playCountDesc",
            MediaOrderingOption.PlayCountAsc => "playCountAsc",
            MediaOrderingOption.LastInteractedDesc => "lastPlayedDesc",
            MediaOrderingOption.LastInteractedAsc => "lastPlayedAsc",
            _ => sort.ToString()
        };

    public static MediaOrderingOption FromUrlValue(string? value) =>
        value switch
        {
            null or "" => MediaOrderingOption.TitleAsc,
            "titleDesc" => MediaOrderingOption.TitleDesc,
            "createdDesc" => MediaOrderingOption.CreatedDesc,
            "createdAsc" => MediaOrderingOption.CreatedAsc,
            "releaseDateDesc" => MediaOrderingOption.ReleaseDateDesc,
            "releaseDateAsc" => MediaOrderingOption.ReleaseDateAsc,
            "ratingDesc" => MediaOrderingOption.LocalRatingDesc,
            "ratingAsc" => MediaOrderingOption.LocalRatingAsc,
            "playCountDesc" => MediaOrderingOption.PlayCountDesc,
            "playCountAsc" => MediaOrderingOption.PlayCountAsc,
            "lastPlayedDesc" => MediaOrderingOption.LastInteractedDesc,
            "lastPlayedAsc" => MediaOrderingOption.LastInteractedAsc,
            _ => Enum.TryParse<MediaOrderingOption>(value, ignoreCase: true, out var parsed)
                ? parsed
                : MediaOrderingOption.TitleAsc
        };

    public static (SmartPlaylistOrderBy OrderBy, bool Descending) ToSmartPlaylistOrder(
        MediaOrderingOption sort,
        MediaType mediaType)
    {
        var (orderBy, descending) = sort switch
        {
            MediaOrderingOption.TitleDesc => (SmartPlaylistOrderBy.Title, true),
            MediaOrderingOption.TitleAsc => (SmartPlaylistOrderBy.Title, false),
            MediaOrderingOption.CreatedAsc => (SmartPlaylistOrderBy.DateAdded, false),
            MediaOrderingOption.CreatedDesc => (SmartPlaylistOrderBy.DateAdded, true),
            MediaOrderingOption.ReleaseDateAsc => (SmartPlaylistOrderBy.Year, false),
            MediaOrderingOption.ReleaseDateDesc => (SmartPlaylistOrderBy.Year, true),
            MediaOrderingOption.LocalRatingAsc => (SmartPlaylistOrderBy.Rating, false),
            MediaOrderingOption.LocalRatingDesc => (SmartPlaylistOrderBy.Rating, true),
            MediaOrderingOption.PlayCountAsc => (SmartPlaylistOrderBy.PlayCount, false),
            MediaOrderingOption.PlayCountDesc => (SmartPlaylistOrderBy.PlayCount, true),
            MediaOrderingOption.LastInteractedAsc => (SmartPlaylistOrderBy.LastPlayed, false),
            MediaOrderingOption.LastInteractedDesc => (SmartPlaylistOrderBy.LastPlayed, true),
            _ => (SmartPlaylistOrderBy.DateAdded, true)
        };

        return (SmartPlaylistOrderByCatalog.Normalize(orderBy, mediaType), descending);
    }
}
