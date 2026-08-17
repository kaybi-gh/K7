using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Enums;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// Outcome of an episode playback attempt.
/// </summary>
internal enum EpisodePlaybackResult
{
    /// <summary>Playback started.</summary>
    Started,

    /// <summary>No local or remote file is attached to the episode.</summary>
    NotPlayable,

    /// <summary>A local file exists but has not been probed yet, so codecs are unknown.</summary>
    AwaitingProbe
}

internal static class SeriePlaybackHelper
{
    public static bool IsInProgress(LiteSerieEpisodeDto? episode) =>
        episode?.UserState is { IsCompleted: false }
        && (episode.UserState.LastPlaybackPosition >= 1
            || episode.UserState is { ProgressPercentage: >= 1 and < 100 });

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
                && (e.UserState.LastPlaybackPosition >= 1
                    || e.UserState is { ProgressPercentage: >= 1 and < 100 }))
            .OrderByDescending(e => e.UserState?.LastInteractedAt ?? DateTime.MinValue)
            .FirstOrDefault();

        if (inProgress is not null)
            return inProgress;

        var nextUnwatched = allEpisodes.FirstOrDefault(e => e.UserState?.IsCompleted != true);
        return nextUnwatched ?? allEpisodes[0];
    }

    public static async Task<EpisodePlaybackResult> PlayEpisodeAsync(
        LiteSerieEpisodeDto episode,
        Guid serieId,
        IMediaService mediaService,
        IPlayerService playerService,
        PlaybackProgressTracker progressTracker,
        IFeatureAccessService featureAccess,
        IFederationService federationService,
        IK7ServerService apiClient,
        bool fromBeginning = false,
        CancellationToken cancellationToken = default)
    {
        var episodeMedia = await mediaService.GetMediaAsync(episode.Id, cancellationToken, bypassCache: true);
        if (episodeMedia is not SerieEpisodeDto episodeDto)
            return EpisodePlaybackResult.NotPlayable;

        double? startPosition = null;
        if (!fromBeginning
            && await featureAccess.HasCapabilityAsync(Capability.CanResumePlayback)
            && episodeDto.UserState is { LastPlaybackPosition: >= 1, IsCompleted: false })
        {
            startPosition = episodeDto.UserState.LastPlaybackPosition;
        }
        else if (fromBeginning)
        {
            startPosition = 0;
        }

        var indexedFile = episodeDto.IndexedFiles?.FirstOrDefault();
        if (indexedFile is not null)
        {
            var videoMetadata = indexedFile.FileMetadata as VideoFileMetadataDto;
            if (videoMetadata is null)
                return EpisodePlaybackResult.AwaitingProbe;

            progressTracker.StartTracking(
                episode.Id,
                await featureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress),
                serieId,
                indexedFile.Id);

            var episodeTitle = VideoPlayerTitleHelper.FormatEpisode(episodeDto);
            var coverUrl = GetEpisodeStillUrl(episode, apiClient);

            try
            {
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
                    startPosition,
                    videoMetadata.Chapters);
            }
            catch (Exception ex) when (PlaybackErrorHelper.IsMediaNotReady(ex))
            {
                // Cached metadata said the file was playable, but the server has not probed it yet.
                return EpisodePlaybackResult.AwaitingProbe;
            }

            return EpisodePlaybackResult.Started;
        }

        var remoteFile = episodeDto.RemoteIndexedFiles?.FirstOrDefault();
        if (remoteFile is null)
            return EpisodePlaybackResult.NotPlayable;

        progressTracker.StartTracking(
            episode.Id,
            await featureAccess.HasCapabilityAsync(Capability.CanReportPlaybackProgress),
            serieId);

        var epTitle = VideoPlayerTitleHelper.FormatEpisode(episodeDto);
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
            remoteVideoMetadata?.Thumbnails?.Uri?.ToString(),
            episode.Id,
            epTitle,
            cover,
            startPosition);

        return EpisodePlaybackResult.Started;
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
