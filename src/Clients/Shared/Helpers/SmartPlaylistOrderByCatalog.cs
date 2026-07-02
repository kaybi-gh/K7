using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Helpers;

public static class SmartPlaylistOrderByCatalog
{
    private static readonly IReadOnlyList<SmartPlaylistOrderBy> CommonOptions =
    [
        SmartPlaylistOrderBy.DateAdded,
        SmartPlaylistOrderBy.Title,
        SmartPlaylistOrderBy.Year,
        SmartPlaylistOrderBy.PlayCount,
        SmartPlaylistOrderBy.Rating,
        SmartPlaylistOrderBy.LastPlayed,
        SmartPlaylistOrderBy.Random
    ];

    private static readonly IReadOnlyList<SmartPlaylistOrderBy> MusicOptions =
    [
        SmartPlaylistOrderBy.ArtistName,
        SmartPlaylistOrderBy.AlbumTitle,
        SmartPlaylistOrderBy.TrackNumber,
        SmartPlaylistOrderBy.Duration
    ];

    public static IReadOnlyList<SmartPlaylistOrderBy> GetOptions(MediaType mediaType) =>
        mediaType switch
        {
            MediaType.MusicTrack => [.. CommonOptions, .. MusicOptions],
            _ => CommonOptions
        };

    public static SmartPlaylistOrderBy Normalize(SmartPlaylistOrderBy orderBy, MediaType mediaType)
    {
        var options = GetOptions(mediaType);
        return options.Contains(orderBy) ? orderBy : SmartPlaylistOrderBy.DateAdded;
    }
}
