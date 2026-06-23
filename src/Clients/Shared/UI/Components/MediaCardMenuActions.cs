using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.UI.Components;

internal static class MediaCardMenuActions
{
    public static MediaType? InferMediaType(MediaCardViewModel? model) =>
        model is null ? null : model.MediaType ?? InferMediaType(model.Kind);

    public static MediaType? InferMediaType(MediaCardKind kind) => kind switch
    {
        MediaCardKind.Poster => MediaType.Movie,
        MediaCardKind.Serie => MediaType.Serie,
        MediaCardKind.Season => MediaType.SerieSeason,
        MediaCardKind.Episode => MediaType.SerieEpisode,
        _ => null
    };

    public static bool SupportsPlaylist(MediaType? mediaType) =>
        mediaType is MediaType.MusicTrack or MediaType.MusicAlbum or MediaType.MusicArtist
            or MediaType.Movie or MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode;

    public static bool SupportsCollection(MediaType? mediaType) =>
        mediaType is MediaType.Movie or MediaType.MusicAlbum or MediaType.MusicTrack
            or MediaType.MusicArtist or MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode;
}
