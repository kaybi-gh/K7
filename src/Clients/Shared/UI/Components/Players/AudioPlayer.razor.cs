using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class AudioPlayer : IAsyncDisposable
{
    private DotNetObjectReference<AudioPlayer>? _dotNetRef;
    private bool _disposed;
    private bool _isInitialized;
    private Guid? _lastMediaSessionTrackId;
    private double _lastPositionUpdate;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || !System.OperatingSystem.IsBrowser()) return;

        if (AudioPlayerService.IsVisible && !_isInitialized)
        {
            _dotNetRef ??= DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("initAudioPlayer", _dotNetRef);
            await JSRuntime.InvokeVoidAsync("K7.setupMediaSessionActions", _dotNetRef);
            await JSRuntime.InvokeVoidAsync("K7.initKeyboardShortcuts", _dotNetRef);
            _isInitialized = true;

            // Apply persisted volume state
            var deviceType = await DeviceService.GetDeviceTypeAsync();
            if (deviceType is DeviceType.Phone or DeviceType.Tablet)
            {
                await JSRuntime.InvokeVoidAsync("audioSetVolume", 1.0);
                await JSRuntime.InvokeVoidAsync("audioSetMuted", false);
            }
            else
            {
                await JSRuntime.InvokeVoidAsync("audioSetVolume", AudioPlayerService.Volume);
                await JSRuntime.InvokeVoidAsync("audioSetMuted", AudioPlayerService.IsMuted);
            }
            await JSRuntime.InvokeVoidAsync("audioSetCrossfadeDuration", AudioPlayerService.CrossfadeTriggerWindow);
            await JSRuntime.InvokeVoidAsync("audioSetKeepScreenOn", AudioPlayerService.KeepScreenOn);
        }
    }

    protected override void OnInitialized()
    {
        if (_disposed || !System.OperatingSystem.IsBrowser()) return;

        AudioPlayerService.PlayRequested += PlayAsync;
        AudioPlayerService.PauseRequested += PauseAsync;
        AudioPlayerService.StopRequested += StopAsync;
        AudioPlayerService.SeekRequested += SeekAsync;
        AudioPlayerService.MuteRequested += MuteAsync;
        AudioPlayerService.UnmuteRequested += UnmuteAsync;
        AudioPlayerService.VolumeChangeRequested += SetVolumeAsync;
        AudioPlayerService.SourceChanged += OnSourceChanged;
        AudioPlayerService.IsVisibleChanged += OnVisibilityChanged;
        AudioPlayerService.CrossfadeRequested += OnCrossfadeRequested;
        AudioPlayerService.GaplessPrebufferRequested += OnGaplessPrebufferRequested;
        AudioPlayerService.LoudnessSettingsChanged += OnLoudnessSettingsChanged;
        AudioPlayerService.EqSettingsChanged += OnEqSettingsChanged;
        AudioPlayerService.CrossfadeDurationChanged += OnCrossfadeDurationChanged;
        AudioPlayerService.PlayerUxSettingsChanged += OnPlayerUxSettingsChanged;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (!System.OperatingSystem.IsBrowser()) return;

        AudioPlayerService.PlayRequested -= PlayAsync;
        AudioPlayerService.PauseRequested -= PauseAsync;
        AudioPlayerService.StopRequested -= StopAsync;
        AudioPlayerService.SeekRequested -= SeekAsync;
        AudioPlayerService.MuteRequested -= MuteAsync;
        AudioPlayerService.UnmuteRequested -= UnmuteAsync;
        AudioPlayerService.VolumeChangeRequested -= SetVolumeAsync;
        AudioPlayerService.SourceChanged -= OnSourceChanged;
        AudioPlayerService.IsVisibleChanged -= OnVisibilityChanged;
        AudioPlayerService.CrossfadeRequested -= OnCrossfadeRequested;
        AudioPlayerService.GaplessPrebufferRequested -= OnGaplessPrebufferRequested;
        AudioPlayerService.LoudnessSettingsChanged -= OnLoudnessSettingsChanged;
        AudioPlayerService.EqSettingsChanged -= OnEqSettingsChanged;
        AudioPlayerService.CrossfadeDurationChanged -= OnCrossfadeDurationChanged;
        AudioPlayerService.PlayerUxSettingsChanged -= OnPlayerUxSettingsChanged;

        if (_isInitialized)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("K7.disposeKeyboardShortcuts");
                await JSRuntime.InvokeVoidAsync("disposeAudioPlayer");
            }
            catch (JSDisconnectedException) { }
            catch (ObjectDisposedException) { }
        }

        _dotNetRef?.Dispose();
    }

    [JSInvokable]
    public void OnTimeUpdated(double currentTime)
    {
        AudioPlayerService.CurrentTime = currentTime;
        UpdateMediaSessionIfNeeded();
    }

    [JSInvokable]
    public void OnDurationChanged(double duration)
    {
        AudioPlayerService.Duration = duration;
    }

    [JSInvokable]
    public void OnBufferedUpdated(double bufferedEnd)
    {
        AudioPlayerService.BufferedTime = bufferedEnd;
    }

    [JSInvokable]
    public void OnVolumeChanged(double volume, bool muted)
    {
        AudioPlayerService.Volume = volume;
        AudioPlayerService.IsMuted = muted;
    }

    [JSInvokable]
    public void OnPlaybackStateChanged(string state)
    {
        AudioPlayerService.PlaybackState = state switch
        {
            "playing" => PlaybackState.Playing,
            "paused" => PlaybackState.Paused,
            "buffering" => PlaybackState.Buffering,
            _ => PlaybackState.Unknown
        };
    }

    [JSInvokable]
    public async Task OnTrackEnded()
    {
        await AudioPlayerService.OnTrackEndedAsync();
    }

    [JSInvokable]
    public async Task OnCrossfadeNeeded()
    {
        await AudioPlayerService.OnCrossfadeNeededAsync();
    }

    [JSInvokable]
    public async Task OnGaplessPrebufferNeeded()
    {
        await AudioPlayerService.OnGaplessPrebufferNeededAsync();
    }

    private async Task OnGaplessPrebufferRequested(PlayerSource source)
    {
        if (!_isInitialized || string.IsNullOrEmpty(source.Url)) return;
        await JSRuntime.InvokeVoidAsync("audioGaplessPrebuffer", source.Url, source.MimeType);
    }

    private async Task OnCrossfadeRequested(PlayerSource source, double duration)
    {
        if (!_isInitialized || string.IsNullOrEmpty(source.Url)) return;
        await JSRuntime.InvokeVoidAsync("audioStartCrossfade", source.Url, source.MimeType, duration);
    }

    private async Task PlayAsync()
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioPlay");
    }

    private async Task PauseAsync()
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioPause");
    }

    private async Task StopAsync()
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioStop");
    }

    private async Task SeekAsync(double time)
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioSeek", time);
    }

    private async Task MuteAsync()
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioSetMuted", true);
    }

    private async Task UnmuteAsync()
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioSetMuted", false);
    }

    private async Task SetVolumeAsync(double volume)
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioSetVolume", volume);
    }

    private void OnSourceChanged(PlayerSource source) => OnSourceChangedAsync(source).FireAndForget(Logger);

    private async Task OnSourceChangedAsync(PlayerSource source)
    {
        if (_disposed || string.IsNullOrEmpty(source.Url)) return;

        if (!_isInitialized)
        {
            _dotNetRef ??= DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("initAudioPlayer", _dotNetRef);
            await JSRuntime.InvokeVoidAsync("K7.setupMediaSessionActions", _dotNetRef);
            await JSRuntime.InvokeVoidAsync("K7.initKeyboardShortcuts", _dotNetRef);
            _isInitialized = true;

            await JSRuntime.InvokeVoidAsync("audioSetVolume", AudioPlayerService.Volume);
            await JSRuntime.InvokeVoidAsync("audioSetMuted", AudioPlayerService.IsMuted);
            await JSRuntime.InvokeVoidAsync("audioSetCrossfadeDuration", AudioPlayerService.CrossfadeTriggerWindow);
            await PushLoudnessSettingsAsync();
            await PushEqSettingsAsync();
        }

        // Push per-track loudness data for normalization
        await PushTrackLoudnessAsync();

        await JSRuntime.InvokeVoidAsync("audioChangeSource", source.Url, source.MimeType);
        await InvokeAsync(StateHasChanged);
    }

    private void OnVisibilityChanged() => OnVisibilityChangedAsync().FireAndForget(Logger);

    private async Task OnVisibilityChangedAsync()
    {
        await InvokeAsync(StateHasChanged);
    }

    private void OnLoudnessSettingsChanged() => OnLoudnessSettingsChangedAsync().FireAndForget(Logger);

    private async Task OnLoudnessSettingsChangedAsync()
    {
        await PushLoudnessSettingsAsync();
    }

    private void OnEqSettingsChanged() => OnEqSettingsChangedAsync().FireAndForget(Logger);

    private async Task OnEqSettingsChangedAsync()
    {
        await PushEqSettingsAsync();
    }

    private void OnCrossfadeDurationChanged() => OnCrossfadeDurationChangedAsync().FireAndForget(Logger);

    private async Task OnCrossfadeDurationChangedAsync()
    {
        if (!_isInitialized) return;
        try
        {
            await JSRuntime.InvokeVoidAsync("audioSetCrossfadeDuration", AudioPlayerService.CrossfadeTriggerWindow);
        }
        catch (JSDisconnectedException) { }
    }

    private void OnPlayerUxSettingsChanged() => OnPlayerUxSettingsChangedAsync().FireAndForget(Logger);

    private async Task OnPlayerUxSettingsChangedAsync()
    {
        if (!_isInitialized) return;
        try
        {
            await JSRuntime.InvokeVoidAsync("audioSetKeepScreenOn", AudioPlayerService.KeepScreenOn);
        }
        catch (JSDisconnectedException) { }
    }

    // --- MediaSession API ---

    private void UpdateMediaSessionIfNeeded() => UpdateMediaSessionIfNeededAsync().FireAndForget(Logger);

    private async Task UpdateMediaSessionIfNeededAsync()
    {
        if (!_isInitialized) return;

        var track = AudioPlayerService.CurrentTrack;

        // Update metadata when track changes
        if (track?.MediaId != _lastMediaSessionTrackId)
        {
            _lastMediaSessionTrackId = track?.MediaId;
            try
            {
                await JSRuntime.InvokeVoidAsync("K7.updateMediaSession",
                    track?.Title, track?.Artist, track?.AlbumTitle, track?.CoverUrl);
            }
            catch (JSDisconnectedException) { }
        }

        // Update position state periodically (every ~1s)
        var now = AudioPlayerService.CurrentTime;
        if (Math.Abs(now - _lastPositionUpdate) >= 1.0)
        {
            _lastPositionUpdate = now;
            try
            {
                await JSRuntime.InvokeVoidAsync("K7.updateMediaSessionPosition",
                    now, AudioPlayerService.Duration, 1.0);
            }
            catch (JSDisconnectedException) { }
        }
    }

    [JSInvokable]
    public void OnMediaSessionPlay() => AudioPlayerService.Play();

    [JSInvokable]
    public void OnMediaSessionPause() => AudioPlayerService.Pause();

    [JSInvokable]
    public async Task OnMediaSessionPrevious() => await AudioPlayerService.PreviousAsync();

    [JSInvokable]
    public async Task OnMediaSessionNext() => await AudioPlayerService.NextAsync();

    [JSInvokable]
    public void OnMediaSessionSeek(double time) => AudioPlayerService.Seek(time);

    // --- Keyboard shortcuts ---

    [JSInvokable]
    public async Task OnKeyboardAction(string action)
    {
        if (!AudioPlayerService.IsVisible) return;

        switch (action)
        {
            case "PlayPause":
                if (AudioPlayerService.PlaybackState == PlaybackState.Playing)
                    AudioPlayerService.Pause();
                else
                    AudioPlayerService.Play();
                break;
            case "SeekForward":
                AudioPlayerService.Seek(Math.Min(AudioPlayerService.CurrentTime + AudioPlayerService.SkipForwardSeconds, AudioPlayerService.Duration));
                break;
            case "SeekBackward":
                AudioPlayerService.Seek(Math.Max(AudioPlayerService.CurrentTime - AudioPlayerService.SkipBackSeconds, 0));
                break;
            case "NextTrack":
                await AudioPlayerService.NextAsync();
                break;
            case "PreviousTrack":
                await AudioPlayerService.PreviousAsync();
                break;
            case "ToggleMute":
                if (AudioPlayerService.IsMuted) AudioPlayerService.Unmute();
                else AudioPlayerService.Mute();
                break;
            case "VolumeUp":
                AudioPlayerService.SetVolume(Math.Min(AudioPlayerService.Volume + 0.05, 1.0));
                break;
            case "VolumeDown":
                AudioPlayerService.SetVolume(Math.Max(AudioPlayerService.Volume - 0.05, 0.0));
                break;
        }
    }

    private async Task PushLoudnessSettingsAsync()
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioSetLoudness",
            AudioPlayerService.LoudnessEnabled,
            AudioPlayerService.LoudnessTargetLufs,
            AudioPlayerService.LoudnessPreampDb,
            AudioPlayerService.LimiterEnabled);
    }

    private async Task PushTrackLoudnessAsync()
    {
        if (!_isInitialized) return;
        var track = AudioPlayerService.CurrentTrack;
        await JSRuntime.InvokeVoidAsync("audioSetTrackLoudness",
            track?.LoudnessLufs,
            track?.ReplayGainTrackGain);
    }

    private async Task PushEqSettingsAsync()
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioSetEq",
            AudioPlayerService.EqEnabled,
            AudioPlayerService.EqBands);
    }
}
