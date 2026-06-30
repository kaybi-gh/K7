using K7.Server.Domain.Enums;

namespace K7.Shared.Navigation;

public static class MediaPageUrls
{
    public static string? Build(
        MediaType type,
        Guid id,
        Guid? serieId = null,
        int? seasonNumber = null,
        int? episodeNumber = null,
        Guid? albumId = null) => type switch
    {
        MediaType.Movie => $"/movies/{id}",
        MediaType.Serie => $"/series/{id}",
        MediaType.SerieSeason when serieId is not null && seasonNumber is not null
            => $"/series/{serieId}/seasons/{seasonNumber}",
        MediaType.SerieEpisode when serieId is not null && seasonNumber is not null && episodeNumber is not null
            => $"/series/{serieId}/seasons/{seasonNumber}#ep-{episodeNumber}",
        MediaType.MusicAlbum => $"/music/albums/{id}",
        MediaType.MusicTrack when albumId is not null => $"/music/albums/{albumId}",
        MediaType.MusicArtist => $"/music/artists/{id}",
        _ => null
    };

    public static string? BuildFromTypeName(
        string? typeName,
        Guid mediaId,
        Guid? serieId = null,
        int? seasonNumber = null,
        int? episodeNumber = null,
        Guid? albumId = null) =>
        Enum.TryParse<MediaType>(typeName, out var type)
            ? Build(type, mediaId, serieId, seasonNumber, episodeNumber, albumId)
            : null;
}
