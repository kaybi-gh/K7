using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public class AutoplayService : IDisposable
{
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IServerInfoService _serverInfoService;
    private readonly IDeviceStorageService _deviceStorageService;
    private bool _isLoading;

    public bool Enabled { get; private set; }

    public AutoplayService(
        IAudioPlayerService audioPlayerService,
        IServerInfoService serverInfoService,
        IDeviceStorageService deviceStorageService)
    {
        _audioPlayerService = audioPlayerService;
        _serverInfoService = serverInfoService;
        _deviceStorageService = deviceStorageService;
        Enabled = deviceStorageService.Get(PreferenceKeys.AUTOPLAY_ENABLED, true);

        _audioPlayerService.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        _deviceStorageService.Set(PreferenceKeys.AUTOPLAY_ENABLED, enabled);
    }

    private async void OnPlaybackStateChanged(PlaybackState state)
    {
        if (state != PlaybackState.Ended || !Enabled || _isLoading)
            return;

        var currentTrack = _audioPlayerService.CurrentTrack;
        if (currentTrack is null)
            return;

        _isLoading = true;
        try
        {
            var radioTracks = await _serverInfoService.GetMusicRadioAsync(
                "sonic",
                seedTrackId: currentTrack.MediaId,
                limit: 25);

            if (radioTracks is null or { Count: 0 })
                return;

            var queueItems = radioTracks
                .OfType<MusicTrackDto>()
                .Where(t => t.IndexedFiles is { Count: > 0 })
                .Select(MapToQueueItem)
                .ToList();

            if (queueItems.Count == 0)
                return;

            await _audioPlayerService.PlayTracksAsync(queueItems);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private static AudioQueueItem MapToQueueItem(MusicTrackDto track)
    {
        return new AudioQueueItem
        {
            MediaId = track.Id,
            IndexedFileId = track.IndexedFiles!.First().Id,
            Title = track.Title ?? "Unknown",
            Artist = track.ArtistName,
            AlbumTitle = null,
            CoverUrl = track.Pictures?.FirstOrDefault()?.GetUri(MetadataPictureSize.Small)?.OriginalString,
            Duration = (track.IndexedFiles!.First().FileMetadata as AudioFileMetadataDto)?.Duration.TotalSeconds,
            Bpm = track.Bpm,
            Energy = track.Energy,
            MusicalKey = track.MusicalKey,
            LoudnessLufs = track.LoudnessLufs,
            FadeInDuration = track.FadeInDuration,
            FadeOutDuration = track.FadeOutDuration,
            ReplayGainTrackGain = track.ReplayGainTrackGain
        };
    }

    public void Dispose()
    {
        _audioPlayerService.PlaybackStateChanged -= OnPlaybackStateChanged;
    }
}
