using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class VideoPlayer : IAsyncDisposable
{
    private ElementReference _player;
    private ElementReference _videoContainer;
    private DotNetObjectReference<VideoPlayer>? _dotNetRef;
    private bool _isInitialized;
    private bool _playPending;
    private string? _lastPlayerId;
    
    [Parameter] public string SourceUri { get; set; } = string.Empty;
    [Parameter] public string SourceMimeType { get; set; } = string.Empty;
    [Parameter] public string? ThumbnailsSource { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (DeviceService.GetClientType() == ClientType.Web)
        {
            if (PlayerService.IsVisible && !_isInitialized)
            {
                var options = new
                {
                    volume = PlayerService.Volume,
                    muted = PlayerService.IsMuted,
                    autoplay = _playPending
                };

                _dotNetRef ??= DotNetObjectReference.Create(this);

                await JSRuntime.InvokeVoidAsync("initVideoJs", _player.Id, _player, _videoContainer, options, _dotNetRef);
                _isInitialized = true;
                _lastPlayerId = _player.Id;

                if (_playPending && !string.IsNullOrEmpty(_player.Id))
                {
                    _playPending = false;
                    await JSRuntime.InvokeVoidAsync("play", _player.Id);
                }
            }
            else if (!PlayerService.IsVisible && _isInitialized)
            {
                if (!string.IsNullOrEmpty(_lastPlayerId))
                {
                    try
                    {
                        await JSRuntime.InvokeVoidAsync("disposeVideoJs", _lastPlayerId);
                    }
                    catch (JSDisconnectedException)
                    {
                    }
                    catch (ObjectDisposedException)
                    {
                    }
                }

                _isInitialized = false;
                _playPending = false;
                _lastPlayerId = null;
            }
        }
    }

    protected override void OnInitialized()
    {
        PlayerService.SourceChanged += OnSourceChange;
        PlayerService.IsVisibleChanged += StateHasChanged;

        if (DeviceService.GetClientType() == ClientType.Web)
        {
            PlayerService.PlayRequested += PlayAsync;
            PlayerService.PauseRequested += PauseAsync;
            PlayerService.MuteRequested += MuteAsync;
            PlayerService.UnmuteRequest += UnmuteAsync;
            PlayerService.VolumeChangeRequested += SetVolumeAsync;
            PlayerService.PlaybackRateChangeRequested += SetPlaybackRateAsync;
            PlayerService.StopRequested += StopAsync;
            PlayerService.EnterFullScreenRequested += EnterFullScreenAsync;
            PlayerService.ExitFullScreenRequested += ExitFullScreenAsync;
            PlayerService.SeekRequested += SeekAsync;
            PlayerService.SwitchAudioTrackRequested += OnSwitchAudioTrack;
            PlayerService.SwitchSubtitleTrackRequested += OnSwitchSubtitleTrack;
            PlayerService.AspectRatioModeChangeRequested += OnAspectRatioModeChange;
        }
    }

    public async ValueTask DisposeAsync()
    {
        PlayerService.SourceChanged -= OnSourceChange;
        PlayerService.IsVisibleChanged -= StateHasChanged;

        if (DeviceService.GetClientType() == ClientType.Web)
        {
            PlayerService.PlayRequested -= PlayAsync;
            PlayerService.PauseRequested -= PauseAsync;
            PlayerService.MuteRequested -= MuteAsync;
            PlayerService.UnmuteRequest -= UnmuteAsync;
            PlayerService.VolumeChangeRequested -= SetVolumeAsync;
            PlayerService.PlaybackRateChangeRequested -= SetPlaybackRateAsync;
            PlayerService.StopRequested -= StopAsync;
            PlayerService.EnterFullScreenRequested -= EnterFullScreenAsync;
            PlayerService.ExitFullScreenRequested -= ExitFullScreenAsync;
            PlayerService.SeekRequested -= SeekAsync;
            PlayerService.SwitchAudioTrackRequested -= OnSwitchAudioTrack;
            PlayerService.SwitchSubtitleTrackRequested -= OnSwitchSubtitleTrack;
            PlayerService.AspectRatioModeChangeRequested -= OnAspectRatioModeChange;

            if (!string.IsNullOrEmpty(_lastPlayerId))
            {
                try
                {
                    await JSRuntime.InvokeVoidAsync("disposeVideoJs", _lastPlayerId);
                }
                catch (JSDisconnectedException)
                {
                }
                catch (ObjectDisposedException)
                {
                }
            }
        }

        _dotNetRef?.Dispose();
        _dotNetRef = null;
    }

    private async void OnSourceChange(PlayerSource playerSource)
    {
        SourceUri = playerSource.Url!;
        SourceMimeType = playerSource.MimeType!;
        ThumbnailsSource = playerSource.ThumbnailsUrl;

        if (DeviceService.GetClientType() == ClientType.Web && _isInitialized && !string.IsNullOrEmpty(_player.Id))
        {
            if (playerSource.PendingSeekTime is double seekTime)
            {
                await JSRuntime.InvokeVoidAsync("changeSourceAndSeek", _player.Id, SourceUri, SourceMimeType, seekTime);
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("changeSource", _player.Id, SourceUri, SourceMimeType);
            }
        }

        await InvokeAsync(StateHasChanged);
    }

    private async void OnSwitchAudioTrack(string trackName)
    {
        if (_isInitialized && !string.IsNullOrEmpty(_player.Id))
        {
            await JSRuntime.InvokeVoidAsync("switchAudioTrack", _player.Id, trackName);
        }
    }

    private async void OnSwitchSubtitleTrack(string? slug)
    {
        if (_isInitialized && !string.IsNullOrEmpty(_player.Id))
        {
            await JSRuntime.InvokeVoidAsync("switchSubtitleTrack", _player.Id, slug);
        }
    }

    private async void OnAspectRatioModeChange(AspectRatioMode mode)
    {
        if (_isInitialized && !string.IsNullOrEmpty(_player.Id))
        {
            await JSRuntime.InvokeVoidAsync("setAspectRatioMode", _player.Id, mode.ToString());
        }
    }

    public async Task PlayAsync()
    {
        if (_isInitialized && !string.IsNullOrEmpty(_player.Id))
        {
            await JSRuntime.InvokeVoidAsync("play", _player.Id);
        }
        else
        {
            _playPending = true;
        }
    }
    public async Task PauseAsync()
    {
        if (_isInitialized && !string.IsNullOrEmpty(_player.Id))
        {
            await JSRuntime.InvokeVoidAsync("pause", _player.Id);
        }
        else
        {
            _playPending = false;
        }
    }
    
    public async Task StopAsync()
    {
        if (_isInitialized && !string.IsNullOrEmpty(_player.Id))
        {
            await JSRuntime.InvokeVoidAsync("stop", _player.Id);
        }
        else
        {
            _playPending = false;
        }
    }
    public async Task SeekAsync(double seconds) => await JSRuntime.InvokeVoidAsync("seek", _player.Id, seconds);
    public async Task MuteAsync() => await JSRuntime.InvokeVoidAsync("mute", _player.Id);
    public async Task UnmuteAsync() => await JSRuntime.InvokeVoidAsync("unmute", _player.Id);
    public async Task SetVolumeAsync(double volume) => await JSRuntime.InvokeVoidAsync("changeVolume", _player.Id, volume);
    public async Task SetPlaybackRateAsync(double rate) => await JSRuntime.InvokeVoidAsync("changePlaybackRate", _player.Id, rate);

    public async Task<double> GetDurationAsync() => await JSRuntime.InvokeAsync<double>("getDuration", _player.Id);
    public async Task<double> GetCurrentTimeAsync() => await JSRuntime.InvokeAsync<double>("getCurrentTime", _player.Id);
    public async Task<double> GetBufferedTimeAsync() => await JSRuntime.InvokeAsync<double>("getBufferedTime", _player.Id);
    public async Task EnterFullScreenAsync() => await JSRuntime.InvokeVoidAsync("enterFullscreen", _videoContainer);
    public async Task ExitFullScreenAsync() => await JSRuntime.InvokeVoidAsync("exitFullscreen");
    // RemainingTime interesting and available in videoplayer

    [JSInvokable]
    public void OnGenericPlayerEvent(string eventName)
    {
        switch (eventName)
        {
            // Fired when the user agent begins looking for media data
            case "loadstart":
                PlayerService.PlaybackState = PlaybackState.Idle;
                break;

            // Fires when the loading of an audio/video is aborted.
            case "abort":
                PlayerService.PlaybackState = PlaybackState.Idle;
                break;

            // Fires when the browser is trying to get media data, but data is not available.
            case "stalled":
                PlayerService.PlaybackState = PlaybackState.Buffering;
                break;

            // Called when the player is being disposed of.
            case "dispose":
                break;

            // Fires when the current playlist is empty.
            case "emptied":
                PlayerService.PlaybackState = PlaybackState.Idle;
                break;

            // Fires when the browser has loaded the current frame of the audio/video.
            case "loadeddata":
                break;

            // Fires when the browser has loaded meta data for the audio/video.ed.
            case "loadedmetadata":
                break;

            // Triggered when a Component is ready.
            case "ready":
                break;

            // Triggered whenever a play event happens. Indicates that playback has started or resumed
            case "play":
                break;

            // Fired whenever the media has been paused
            case "pause":
                PlayerService.PlaybackState = PlaybackState.Paused;
                break;

            // Fired when the end of the media resource is reached (currentTime == duration)
            case "ended":
                PlayerService.PlaybackState = PlaybackState.Ended;
                break;

            // A readyState change on the DOM element has caused playback to stop.
            case "waiting":
                PlayerService.PlaybackState = PlaybackState.Buffering;
                break;

            // Fired whenever the player is jumping to a new time
            case "seeking":
                break;

            // The media is no longer blocked from playback, and has started playing.
            case "playing":
                PlayerService.PlaybackState = PlaybackState.Playing;
                break;

            // Fired when the player has finished jumping to a new time
            case "seeked":
                break;

            // This event fires when the player enters picture in picture mode
            case "enterpictureinpicture":
                break;

            // This event fires when the player leaves picture in picture mode
            case "leavepictureinpicture":
                break;

            // The media has a readyState of HAVE_FUTURE_DATA or greater.
            case "canplay":
                break;

            // The media has a readyState of HAVE_ENOUGH_DATA or greater. This means that the entire media file can be played without buffering.
            case "canplaythrough":
                break;
        }
    }

    [JSInvokable]
    public void OnDurationChanged(double duration) => PlayerService.Duration = duration;

    [JSInvokable]
    public void OnTimeUpdated(double time) => PlayerService.CurrentTime = time;

    [JSInvokable]
    public void OnBufferedUpdated(double time) => PlayerService.BufferedTime = time;

    [JSInvokable]
    public void OnVolumeChanged(double volume, bool muted)
    {
        PlayerService.Volume = volume;
        PlayerService.IsMuted = muted;
    }

    [JSInvokable]
    public void OnPlaybackRateChanged(double rate) => PlayerService.PlaybackRate = rate;

    [JSInvokable]
    public void OnFullscreenChanged(bool isFullscreen) => PlayerService.IsFullScreen = isFullscreen;
}
