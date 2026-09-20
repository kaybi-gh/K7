using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public sealed class SyncPlayMediaLoader : ISyncPlayMediaLoader
{
    private readonly IMediaService _mediaService;
    private readonly IPlayerService _videoPlayer;
    private readonly IAudioPlayerService _audioPlayer;
    private readonly PlaybackProgressTracker _progressTracker;

    public SyncPlayMediaLoader(IMediaService mediaService, IPlayerService videoPlayer, IAudioPlayerService audioPlayer, PlaybackProgressTracker progressTracker)
    {
        _mediaService = mediaService;
        _videoPlayer = videoPlayer;
        _audioPlayer = audioPlayer;
        _progressTracker = progressTracker;
    }

    public async Task LoadAndPlayMediaAsync(
        Guid mediaReferenceId,
        string? title,
        string? coverUrl,
        double? startPosition = null,
        Guid? indexedFileId = null,
        int? audioTrackIndex = null,
        int? subtitleTrackIndex = null,
        double? playbackRate = null,
        AspectRatioMode? aspectRatio = null,
        double? volume = null)
    {
        var media = await _mediaService.GetMediaAsync(mediaReferenceId);
        if (media is null) return;

        var indexedFile = ResolveIndexedFile(media, indexedFileId);
        if (indexedFile is null) return;

        if (media is MusicTrackDto musicTrack)
        {
            // Stop video player when switching to audio
            if (_videoPlayer.IsVisible)
                await _videoPlayer.HideAsync();

            var queueItem = new AudioQueueItem
            {
                IndexedFileId = indexedFile.Id,
                MediaId = media.Id,
                Title = musicTrack.Title ?? title ?? "Unknown",
                Artist = musicTrack.ArtistName,
                AlbumTitle = null,
                CoverUrl = coverUrl
            };

            await _audioPlayer.PlayTrackAsync(queueItem);
            if (startPosition is > 1)
                _audioPlayer.Seek(startPosition.Value);
            if (volume is >= 0)
                _audioPlayer.SetVolume(Math.Clamp(volume.Value, 0, 1));
        }
        else
        {
            // Any non-music media (movies, episodes) plays in the video player
            if (_audioPlayer.IsVisible)
            {
                _audioPlayer.Stop();
                await _audioPlayer.HideAsync();
            }

            var videoMetadata = indexedFile.FileMetadata as VideoFileMetadataDto;

            await _videoPlayer.PlayIndexedFileAsync(
                indexedFile.Id,
                videoMetadata?.AudioTracks ?? [],
                videoMetadata?.SubtitleTracks,
                audioTrackIndex: audioTrackIndex,
                subtitleTrackIndex: subtitleTrackIndex,
                mediaId: media.Id,
                title: title ?? VideoPlayerTitleHelper.FormatFromMedia(media),
                coverUrl: coverUrl,
                startPosition: startPosition is > 1 ? startPosition : null,
                chapters: videoMetadata?.Chapters,
                durationSeconds: videoMetadata?.Duration.TotalSeconds);

            if (playbackRate is > 0 && Math.Abs(playbackRate.Value - 1) > 0.01)
                _videoPlayer.SetPlaybackRate(playbackRate.Value);
            if (aspectRatio is AspectRatioMode mode)
                _videoPlayer.SetAspectRatioMode(mode);
            if (volume is >= 0)
                _videoPlayer.SetVolume(Math.Clamp(volume.Value, 0, 1));

            var serieId = (media as SerieEpisodeDto)?.SerieId;
            _progressTracker.StartTracking(media.Id, isAuthenticated: true, serieId: serieId, indexedFileId: indexedFile.Id);
        }
    }

    private static IndexedFileDto? ResolveIndexedFile(MediaDto media, Guid? preferredFileId)
    {
        var files = media.IndexedFiles;
        if (files is null || files.Count == 0)
            return null;

        if (preferredFileId is Guid id && id != Guid.Empty)
        {
            var match = files.FirstOrDefault(f => f.Id == id);
            if (match is not null)
                return match;
        }

        return files.FirstOrDefault();
    }

    public async Task LoadQueueAsync(IReadOnlyList<SyncPlayQueueItemDto> queue, int currentIndex)
    {
        var items = new List<AudioQueueItem>();
        var indexMap = new Dictionary<int, int>(); // maps original index to filtered index
        var filteredIndex = 0;

        for (var i = 0; i < queue.Count; i++)
        {
            var queueItem = queue[i];
            var media = await _mediaService.GetMediaAsync(queueItem.MediaReferenceId);
            if (media is null) continue;

            var indexedFile = media.IndexedFiles?.FirstOrDefault();
            if (indexedFile is null) continue;

            if (media is MusicTrackDto musicTrack)
            {
                indexMap[i] = filteredIndex++;
                items.Add(new AudioQueueItem
                {
                    IndexedFileId = indexedFile.Id,
                    MediaId = media.Id,
                    Title = musicTrack.Title ?? queueItem.Title,
                    Artist = musicTrack.ArtistName,
                    AlbumTitle = null,
                    CoverUrl = queueItem.CoverUrl
                });
            }
        }

        if (items.Count > 0)
        {
            if (_videoPlayer.IsVisible)
                await _videoPlayer.HideAsync();

            var mappedIndex = indexMap.TryGetValue(currentIndex, out var mapped) ? mapped : 0;
            await _audioPlayer.PlayTracksAsync(items, mappedIndex);
        }
    }
}
