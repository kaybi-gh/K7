using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.UI.Helpers;

internal static class MediaPlaylistAddHelper
{
    public static MediaType? GetTargetPlaylistMediaType(MediaType? sourceMediaType) => sourceMediaType switch
    {
        MediaType.MusicAlbum or MediaType.MusicArtist or MediaType.MusicTrack => MediaType.MusicTrack,
        MediaType.Movie => MediaType.Movie,
        MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode => MediaType.SerieEpisode,
        _ => null
    };

    public static async Task<IReadOnlyList<Guid>> ResolvePlaylistMediaIdsAsync(
        IMediaService mediaService,
        Guid mediaId,
        MediaType? sourceMediaType,
        CancellationToken cancellationToken = default)
    {
        if (sourceMediaType is MediaType.Movie or MediaType.MusicTrack or MediaType.SerieEpisode)
            return [mediaId];

        if (sourceMediaType == MediaType.MusicAlbum)
        {
            var media = await mediaService.GetMediaAsync(mediaId, cancellationToken);
            if (media is MusicAlbumDto album)
            {
                return (album.Tracks ?? [])
                    .Where(IsPlayable)
                    .Select(track => track.Id)
                    .ToList();
            }

            return [];
        }

        if (sourceMediaType == MediaType.MusicArtist)
        {
            return await GetPlayableTrackIdsAsync(mediaService, mediaId, cancellationToken);
        }

        if (sourceMediaType == MediaType.SerieSeason)
        {
            var media = await mediaService.GetMediaAsync(mediaId, cancellationToken);
            if (media is SerieSeasonDto season)
            {
                return (season.Episodes ?? [])
                    .Where(IsPlayable)
                    .OrderBy(episode => episode.EpisodeNumber)
                    .Select(episode => episode.Id)
                    .ToList();
            }

            return [];
        }

        if (sourceMediaType == MediaType.Serie)
        {
            var media = await mediaService.GetMediaAsync(mediaId, cancellationToken);
            if (media is not SerieDto serie || serie.Seasons is not { Count: > 0 })
                return [];

            var episodeIds = new List<Guid>();
            foreach (var season in serie.Seasons.OrderBy(s => s.SeasonNumber))
            {
                var seasonMedia = await mediaService.GetMediaAsync(season.Id, cancellationToken);
                if (seasonMedia is SerieSeasonDto seasonDto)
                {
                    episodeIds.AddRange((seasonDto.Episodes ?? [])
                        .Where(IsPlayable)
                        .OrderBy(episode => episode.EpisodeNumber)
                        .Select(episode => episode.Id));
                }
            }

            return episodeIds;
        }

        return [mediaId];
    }

    public static async Task<int> AddMediaToPlaylistAsync(
        IPlaylistService playlistService,
        IMediaService mediaService,
        Guid playlistId,
        Guid mediaId,
        MediaType? sourceMediaType,
        CancellationToken cancellationToken = default)
    {
        var mediaIds = await ResolvePlaylistMediaIdsAsync(mediaService, mediaId, sourceMediaType, cancellationToken);
        foreach (var id in mediaIds)
            await playlistService.AddPlaylistItemAsync(playlistId, id, cancellationToken);

        return mediaIds.Count;
    }

    private static async Task<IReadOnlyList<Guid>> GetPlayableTrackIdsAsync(
        IMediaService mediaService,
        Guid artistId,
        CancellationToken cancellationToken)
    {
        var result = await mediaService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicTrack],
            ArtistIds = [artistId],
            OrderBy = [MediaOrderingOption.TitleAsc],
            PageNumber = 1,
            PageSize = 500
        }, cancellationToken);

        return result?.Items?
            .OfType<LiteMusicTrackDto>()
            .Where(IsPlayable)
            .Select(track => track.Id)
            .ToList() ?? [];
    }

    private static bool IsPlayable(LiteMusicTrackDto track) =>
        track.IndexedFileId.HasValue || track.RemoteIndexedFileId.HasValue;

    private static bool IsPlayable(LiteSerieEpisodeDto episode) =>
        episode.IndexedFileId.HasValue || episode.RemoteIndexedFileId.HasValue;
}
