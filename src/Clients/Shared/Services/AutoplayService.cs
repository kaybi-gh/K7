using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public class AutoplayService : IDisposable
{
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IMusicRadioPlaybackService _musicRadioPlayback;
    private readonly IServerPreferencesService _serverPreferences;
    private readonly IDeviceStorageService _deviceStorageService;
    private bool _isLoading;

    public bool Enabled { get; private set; }

    public AutoplayService(
        IAudioPlayerService audioPlayerService,
        IMusicRadioPlaybackService musicRadioPlayback,
        IDeviceStorageService deviceStorageService,
        IServerPreferencesService serverPreferences)
    {
        _audioPlayerService = audioPlayerService;
        _musicRadioPlayback = musicRadioPlayback;
        _deviceStorageService = deviceStorageService;
        _serverPreferences = serverPreferences;
        Enabled = deviceStorageService.Get(PreferenceKeys.AUTOPLAY_ENABLED, true);

        _audioPlayerService.PlaybackStateChanged += OnPlaybackStateChanged;
    }

    public void SetEnabled(bool enabled)
    {
        Enabled = enabled;
        _deviceStorageService.Set(PreferenceKeys.AUTOPLAY_ENABLED, enabled);
    }

    private void OnPlaybackStateChanged(PlaybackState state) => OnPlaybackStateChangedAsync(state).FireAndForget();

    private async Task OnPlaybackStateChangedAsync(PlaybackState state)
    {
        if (state != PlaybackState.Ended || !Enabled || _isLoading)
            return;

        var currentTrack = _audioPlayerService.CurrentTrack;
        if (currentTrack is null)
            return;

        _isLoading = true;
        try
        {
            var status = await _serverPreferences.GetMusicIntelligenceStatusAsync();
            if (!status.IsAvailable)
                return;

            var started = await _musicRadioPlayback.StartAsync(new MusicRadioRequest
            {
                RadioType = "Sonic",
                Title = "Similaire",
                SeedTrackId = currentTrack.MediaId
            });

            if (!started)
                return;
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
