using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Enums;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.UI.Helpers;

internal static class SeriePlaybackHelper
{
    public static async Task<LiteSerieEpisodeDto?> ResolveEpisodeToPlayAsync(
        IMediaService mediaService,
        IReadOnlyList<LiteSerieSeasonDto> seasons,
        CancellationToken cancellationToken = default)
    {
        var allEpisodes = await LoadPlayableEpisodesAsync(mediaService, seasons, cancellationToken);
        if (allEpisodes.Count == 0)
            return null;

        var inProgress = allEpisodes
            .Where(e => e.UserState is { IsCompleted: false }
                && (e.UserState.LastPlaybackPosition > 0
                    || e.UserState is { ProgressPercentage: > 0 and < 100 }))
            .OrderByDescending(e => e.UserState?.LastInteractedAt ?? DateTime.MinValue)
            .FirstOrDefault();

        if (inProgress is not null)
            return inProgress;

        var nextUnwatched = allEpisodes.FirstOrDefault(e => e.UserState?.IsCompleted != true);
        return nextUnwatched ?? allEpisodes[0];
    }

    public static async Task PlayEpisodeAsync(
        LiteSerieEpisodeDto episode,
        Guid serieId,
        IMediaService mediaService,
        IPlayerService playerService,
        PlaybackProgressTracker progressTracker,
        IFeatureAccessService featureAccess,
        IFederationService federationService,
        IK7ServerService apiClient,
        CancellationToken cancellationToken = default)
    {
        var episodeMedia = await mediaService.GetMediaAsync(episode.Id, cancellationToken);
        if (episodeMedia is not SerieEpisodeDto episodeDto)
            return;

        double? startPosition = null;
        if (await featureAccess.HasCapabilityAsync(Capability.CanResumePlayback)
            && episode.UserState is { LastPlaybackPosition: > 0, IsCompleted: false })
        {
            startPosition = episode.UserState.LastPlaybackPosition;
        }

        var indexedFile = episodeDto.IndexedFiles?.FirstOrDefault();
        if (indexedFile is not null)
        {
            var videoMetadata = indexedFile.FileMetadata as VideoFileMetadataDto;
            if (videoMetadata is null)
                return;

            progressTracker.StartTracking(
                episode.Id,
                await featureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress),
                serieId,
                indexedFile.Id);

            var episodeTitle = episode.Title ?? $"S{episode.SeasonNumber:D2}E{episode.EpisodeNumber:D2}";
            var coverUrl = GetEpisodeStillUrl(episode, apiClient);

            await playerService.PlayIndexedFileAsync(
                indexedFile.Id,
                videoMetadata.AudioTracks ?? [],
                videoMetadata.SubtitleTracks,
                videoMetadata.AudioTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
                videoMetadata.SubtitleTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
                videoMetadata.VideoResolution,
                videoMetadata.Thumbnails?.Uri?.ToString(),
                episode.Id,
                episodeTitle,
                coverUrl,
                startPosition);

            return;
        }

        var remoteFile = episodeDto.RemoteIndexedFiles?.FirstOrDefault();
        if (remoteFile is null)
            return;

        progressTracker.StartTracking(
            episode.Id,
            await featureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress),
            serieId);

        var epTitle = episode.Title ?? $"S{episode.SeasonNumber:D2}E{episode.EpisodeNumber:D2}";
        var cover = GetEpisodeStillUrl(episode, apiClient);

        var details = await federationService.GetRemoteFileDetailsAsync(remoteFile.Id, cancellationToken);
        var remoteVideoMetadata = details?.FileMetadata as VideoFileMetadataDto;

        await playerService.PlayRemoteIndexedFileAsync(
            remoteFile.Id,
            remoteVideoMetadata?.AudioTracks ?? [],
            remoteVideoMetadata?.SubtitleTracks,
            remoteVideoMetadata?.AudioTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            remoteVideoMetadata?.SubtitleTracks?.FirstOrDefault(t => t.IsDefault)?.Index,
            remoteVideoMetadata?.VideoResolution,
            episode.Id,
            epTitle,
            cover,
            startPosition);
    }

    private static async Task<List<LiteSerieEpisodeDto>> LoadPlayableEpisodesAsync(
        IMediaService mediaService,
        IReadOnlyList<LiteSerieSeasonDto> seasons,
        CancellationToken cancellationToken)
    {
        var orderedSeasons = seasons
            .OrderBy(s => s.SeasonNumber == 0 ? int.MaxValue : s.SeasonNumber);

        var allEpisodes = new List<LiteSerieEpisodeDto>();
        foreach (var season in orderedSeasons)
        {
            var seasonMedia = await mediaService.GetMediaAsync(season.Id, cancellationToken);
            if (seasonMedia is not SerieSeasonDto seasonDto)
                continue;

            allEpisodes.AddRange((seasonDto.Episodes ?? [])
                .Where(IsPlayable)
                .OrderBy(e => e.EpisodeNumber));
        }

        return allEpisodes;
    }

    private static bool IsPlayable(LiteSerieEpisodeDto episode) =>
        episode.IndexedFileId.HasValue || episode.RemoteIndexedFileId.HasValue;

    private static string? GetEpisodeStillUrl(LiteSerieEpisodeDto episode, IK7ServerService apiClient)
    {
        if (episode.StillImageId is null)
            return null;

        return apiClient.GetAbsoluteUri(
            episode.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Still)
                ?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;
    }
}
