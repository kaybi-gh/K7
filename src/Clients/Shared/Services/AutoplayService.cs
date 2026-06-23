using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public class AutoplayService : IDisposable
{
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IServerInfoService _serverInfoService;
    private readonly IK7ServerService _apiClient;
    private readonly IDeviceStorageService _deviceStorageService;
    private bool _isLoading;

    public bool Enabled { get; private set; }

    public AutoplayService(
        IAudioPlayerService audioPlayerService,
        IServerInfoService serverInfoService,
        IK7ServerService apiClient,
        IDeviceStorageService deviceStorageService)
    {
        _audioPlayerService = audioPlayerService;
        _serverInfoService = serverInfoService;
        _apiClient = apiClient;
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
                .Select(t => MusicTrackQueueMapper.ToQueueItem(t, _apiClient))
                .Where(t => t is not null)
                .Cast<AudioQueueItem>()
                .ToList();

            if (queueItems.Count == 0)
                return;

            await _audioPlayerService.PlayRadioAsync(queueItems, "Similaire");
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void Dispose()
    {
        _audioPlayerService.PlaybackStateChanged -= OnPlaybackStateChanged;
    }
}
