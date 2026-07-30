using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Helpers;

public static class DynamicPlaylistOrderByCatalog
{
    private static readonly IReadOnlyList<DynamicPlaylistOrderBy> CommonOptions =
    [
        DynamicPlaylistOrderBy.DateAdded,
        DynamicPlaylistOrderBy.Title,
        DynamicPlaylistOrderBy.Year,
        DynamicPlaylistOrderBy.PlayCount,
        DynamicPlaylistOrderBy.Rating,
        DynamicPlaylistOrderBy.LastPlayed,
        DynamicPlaylistOrderBy.Random
    ];

    private static readonly IReadOnlyList<DynamicPlaylistOrderBy> MusicOptions =
    [
        DynamicPlaylistOrderBy.ArtistName,
        DynamicPlaylistOrderBy.AlbumTitle,
        DynamicPlaylistOrderBy.TrackNumber,
        DynamicPlaylistOrderBy.Duration
    ];

    public static IReadOnlyList<DynamicPlaylistOrderBy> GetOptions(MediaType mediaType) =>
        mediaType switch
        {
            MediaType.MusicTrack => [.. CommonOptions, .. MusicOptions],
            _ => CommonOptions
        };

    public static DynamicPlaylistOrderBy Normalize(DynamicPlaylistOrderBy orderBy, MediaType mediaType)
    {
        var options = GetOptions(mediaType);
        return options.Contains(orderBy) ? orderBy : DynamicPlaylistOrderBy.DateAdded;
    }
}
