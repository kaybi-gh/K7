using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class VideoPlayer : IAsyncDisposable
{
    private ElementReference _player;
    private ElementReference _videoContainer;
    private DotNetObjectReference<VideoPlayer>? _dotNetRef;
    private bool _isInitialized;
    private bool _initInProgress;
    private bool _playPending;
    private bool _sourceApplyPending;
    private string? _lastPlayerId;
    private bool _syncPlaySidebarOpen;
    private CancellationTokenSource? _durationWaitCts;
    private bool _mediaCanPlay;
    // HLS index.m3u8 exposes duration before segment 0 exists; wait for real media progress.
    // Applies to Web + Windows Video.js only - native MediaElement has no idle watchdog.
    private static readonly TimeSpan DurationReadyTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan WindowsWebDurationReadyTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan RecoveryRoundTimeout = TimeSpan.FromSeconds(30);
    private const int MaxWindowsWebRecoveryRounds = 3;
    // Video.js MEDIA_ERR_SRC_NOT_SUPPORTED - hard failure only, not network stalls while burn-in runs.
    private const int MediaErrSrcNotSupported = 4;
    private DateTime _lastHardPlayerErrorReportUtc = DateTime.MinValue;
    private string? _lastHardPlayerErrorReportKey;
    private static readonly TimeSpan HardPlayerErrorReportDedupeWindow = TimeSpan.FromSeconds(30);
    
    [Parameter] public string SourceUri { get; set; } = string.Empty;
    [Parameter] public string SourceMimeType { get; set; } = string.Empty;
    [Parameter] public string? ThumbnailsSource { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (UsesWebVideoPlayer())
        {
            if (PlayerService.IsVisible && !_isInitialized && !_initInProgress)
            {
                _initInProgress = true;
                try
                {
                    var pendingSeek = PlayerService.Source?.PendingSeekTime;
                    // Never autoplay from the embedded <source> when resuming mid-stream -
                    // that would fetch segment 0 before changeSourceAndSeek can run.
                    var options = new
                    {
                        // K7's own VideoPlayerControlsOverlay is the only UI; never let video.js render its default control bar.
                        controls = false,
                        volume = PlayerService.Volume,
                        muted = PlayerService.IsMuted,
                        autoplay = _playPending && pendingSeek is null
                    };

                    _dotNetRef ??= DotNetObjectReference.Create(this);

                    System.Diagnostics.Debug.WriteLine("[K7-Player] Video.js init starting");
                    try
                    {
                        await JSRuntime.InvokeVoidAsync("initVideoJs", _player.Id, _player, _videoContainer, options, _dotNetRef);
                        System.Diagnostics.Debug.WriteLine("[K7-Player] Video.js init succeeded id=" + _player.Id);
                    }
                    catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
                    {
                        System.Diagnostics.Debug.WriteLine("[K7-Player] Video.js init failed: " + ex.Message);
                        throw;
                    }

                    _isInitialized = true;
                    _lastPlayerId = _player.Id;

                    if (pendingSeek is double seekTime && !string.IsNullOrEmpty(SourceUri))
                    {
                        _playPending = false;
                        _sourceApplyPending = false;
                        await JSRuntime.InvokeVoidAsync("changeSourceAndSeek", _player.Id, SourceUri, SourceMimeType, seekTime);
                    }
                    else if (_sourceApplyPending || !string.IsNullOrEmpty(SourceUri))
                    {
                        _sourceApplyPending = false;
                        var source = PlayerService.Source;
                        if (source?.Url is not null)
                            await ApplySourceAsync(source);
                    }

                    if (_playPending && !string.IsNullOrEmpty(_player.Id))
                    {
                        _playPending = false;
                        await JSRuntime.InvokeVoidAsync("play", _player.Id);
                    }
                }
                finally
                {
                    _initInProgress = false;
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
                _sourceApplyPending = false;
                _lastPlayerId = null;
            }
        }
    }

    protected override void OnInitialized()
    {
        PlayerService.SourceChanged += OnSourceChange;
        PlayerService.IsVisibleChanged += OnVisibilityChanged;
        PlayerService.PlaybackStartFailed += OnPlaybackStartFailed;
        RemoteControl.SessionChanged += OnRemoteSessionChanged;
        RemoteControl.StateChanged += OnRemoteSessionChanged;
        SyncPlay.GroupUpdated += OnSyncPlayGroupUpdated;

        if (UsesWebVideoPlayer())
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

    private void OnRemoteSessionChanged() => InvokeAsync(StateHasChanged);

    private void OnVisibilityChanged()
    {
        StateHasChanged();

        // On MAUI Native, hide app chrome so only the video overlay receives input.
        // On Windows the video renders in WebView2 but still needs native-player-active
        // so sibling shell UI does not compete for hit testing / focus.
        if (DeviceService.GetClientType() == ClientType.Native)
        {
            SetNativePlayerActiveAsync(PlayerService.IsVisible).FireAndForget();
        }
    }

    private async Task SetNativePlayerActiveAsync(bool active)
    {
        try
        {
            await JSRuntime.InvokeVoidAsync(
                "K7.setNativePlayerActive",
                active,
                UsesWebVideoPlayer());
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException)
        {
        }
    }

    private void OnVideoPlayerRootClick()
    {
        System.Diagnostics.Debug.WriteLine("[K7-Player] video-player root click");
    }

    private async Task OnResumeHere()
    {
        var position = RemoteControl.Position;
        await RemoteControl.SendStopAsync();

        PlayerService.Play();
        PlayerService.Seek(position);
    }

    private void ToggleSyncPlaySidebar()
    {
        _syncPlaySidebarOpen = !_syncPlaySidebarOpen;
        StateHasChanged();
    }

    private void OnContainerKeyDown(KeyboardEventArgs e)
    {
        // This handler only fires for events originating in the sidebar
        // (overlay has @onkeydown:stopPropagation so its events don't bubble here).
        // Handle global player shortcuts so they work regardless of focus location.
        var code = string.IsNullOrEmpty(e.Code) ? e.Key : e.Code;
        switch (code)
        {
            case "Space" or " " or "MediaPlayPause" or "MediaPlay" or "MediaPause":
                if (PlayerService.PlaybackState == PlaybackState.Playing)
                    PlayerService.Pause();
                else
                    PlayerService.Play();
                break;
            case "KeyM" or "m" or "M":
                if (PlayerService.IsMuted) PlayerService.Unmute();
                else PlayerService.Mute();
                break;
            case "KeyF" or "f" or "F":
                if (PlayerService.IsFullScreen) PlayerService.ExitFullScreen();
                else PlayerService.EnterFullScreen();
                break;
            case "Escape" or "BrowserBack" or "GoBack":
                ToggleSyncPlaySidebar();
                break;
        }
    }

    private void OnSyncPlayGroupUpdated() => InvokeAsync(() =>
    {
        if (!SyncPlay.IsInGroup && _syncPlaySidebarOpen)
        {
            _syncPlaySidebarOpen = false;
        }

        StateHasChanged();
    });

    public async ValueTask DisposeAsync()
    {
        _durationWaitCts?.Cancel();
        _durationWaitCts?.Dispose();
        _durationWaitCts = null;

        PlayerService.SourceChanged -= OnSourceChange;
        PlayerService.IsVisibleChanged -= OnVisibilityChanged;
        PlayerService.PlaybackStartFailed -= OnPlaybackStartFailed;
        RemoteControl.SessionChanged -= OnRemoteSessionChanged;
        RemoteControl.StateChanged -= OnRemoteSessionChanged;
        SyncPlay.GroupUpdated -= OnSyncPlayGroupUpdated;

        if (DeviceService.GetClientType() == ClientType.Native)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("K7.setNativePlayerActive", false);
            }
            catch (JSDisconnectedException)
            {
            }
            catch (ObjectDisposedException)
            {
            }
        }

        if (UsesWebVideoPlayer())
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

    private void OnSourceChange(PlayerSource playerSource) => OnSourceChangeAsync(playerSource).FireAndForget();

    private async Task OnSourceChangeAsync(PlayerSource playerSource)
    {
        SourceUri = playerSource.Url!;
        SourceMimeType = playerSource.MimeType!;

        if (DeviceService.GetClientType() == ClientType.Native
            && !string.IsNullOrEmpty(playerSource.ThumbnailsUrl)
            && K7ServerService.HttpClient.BaseAddress is not null
            && Uri.TryCreate(playerSource.ThumbnailsUrl, UriKind.RelativeOrAbsolute, out var thumbUri)
            && !thumbUri.IsAbsoluteUri)
        {
            ThumbnailsSource = new Uri(K7ServerService.HttpClient.BaseAddress, playerSource.ThumbnailsUrl).ToString();
        }
        else
        {
            ThumbnailsSource = playerSource.ThumbnailsUrl;
        }

        if (UsesWebVideoPlayer() && !string.IsNullOrEmpty(playerSource.Url))
        {
            if (_isInitialized && !string.IsNullOrEmpty(_player.Id))
            {
                await ApplySourceAsync(playerSource);
            }
            else
            {
                _sourceApplyPending = true;
            }
        }

        if (string.IsNullOrEmpty(playerSource.Url))
        {
            _durationWaitCts?.Cancel();
            await InvokeAsync(StateHasChanged);
            return;
        }

        // Native MediaElement (Android/iOS/MacCatalyst): no duration/idle watchdog.
        // That logic was added with Windows Video.js and false-positive ABR killed working streams.
        if (UsesWebVideoPlayer())
            ScheduleDurationReadyCheck();

        await InvokeAsync(StateHasChanged);
    }

    private async Task ApplySourceAsync(PlayerSource source)
    {
        if (!_isInitialized || string.IsNullOrEmpty(_player.Id) || string.IsNullOrEmpty(source.Url))
            return;

        if (source.PendingSeekTime is double seekTime)
        {
            System.Diagnostics.Debug.WriteLine("[K7-Player] source applied with seek url=" + source.Url + " seek=" + seekTime);
            await JSRuntime.InvokeVoidAsync("changeSourceAndSeek", _player.Id, source.Url, source.MimeType ?? SourceMimeType, seekTime);
            return;
        }

        var subtitleSlug = PlayerService.SelectedSubtitleTrack is { IsTextBased: true } sub
            ? $"sub-{sub.Index}"
            : null;
        System.Diagnostics.Debug.WriteLine("[K7-Player] source applied url=" + source.Url);
        await JSRuntime.InvokeVoidAsync("changeSource", _player.Id, source.Url, source.MimeType ?? SourceMimeType, subtitleSlug);
    }

    private bool UsesWebVideoPlayer() =>
        DeviceService.GetClientType() == ClientType.Web
        || (DeviceService.GetClientType() == ClientType.Native
            && WindowsVideoPlayback.UsesWebVideoPlayer);

    private static bool IsFinitePositive(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value > 0;

    private bool IsPlaybackReady()
    {
        // Duration alone is NOT ready: HLS playlists report duration as soon as index.m3u8
        // loads, which is often several seconds before burn-in produces segment 0.
        if (IsFinitePositive(PlayerService.CurrentTime)
            || IsFinitePositive(PlayerService.BufferedTime)
            || PlayerService.PlaybackState is PlaybackState.Playing)
            return true;

        // canplay without buffered media is common during HLS startup; require buffer too.
        if (_mediaCanPlay && IsFinitePositive(PlayerService.BufferedTime))
            return true;

        // Windows MAUI Video.js can enter Buffering on play before HLS is playable.
        // Do not treat Buffering alone as ready so the startup watchdog still fires.
        return false;
    }

    private bool HasPlaybackStartProgress(double lastBuffered) =>
        IsFinitePositive(PlayerService.BufferedTime) && PlayerService.BufferedTime > lastBuffered
        || IsFinitePositive(PlayerService.CurrentTime)
        || PlayerService.PlaybackState is PlaybackState.Playing;

    private void ScheduleDurationReadyCheck()
    {
        _durationWaitCts?.Cancel();
        _durationWaitCts?.Dispose();
        _durationWaitCts = new CancellationTokenSource();
        _mediaCanPlay = false;
        _ = WaitForDurationReadyAsync(_durationWaitCts.Token);
    }

    private async Task WaitForDurationReadyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var isWindowsWebPlayer = UsesWebVideoPlayer() && WindowsVideoPlayback.UsesWebVideoPlayer;
            var maxRounds = isWindowsWebPlayer ? MaxWindowsWebRecoveryRounds : 1;
            var waitTimeout = isWindowsWebPlayer
                ? WindowsWebDurationReadyTimeout
                : DurationReadyTimeout;

            for (var round = 0; round < maxRounds; round++)
            {
                // First chosen quality gets the full window; later ladder steps use the recovery window.
                var roundTimeout = round > 0 ? RecoveryRoundTimeout : waitTimeout;

                var deadline = DateTime.UtcNow + roundTimeout;
                var lastBuffered = PlayerService.BufferedTime;

                while (DateTime.UtcNow < deadline)
                {
                    if (IsPlaybackReady() || !PlayerService.IsVisible)
                        return;

                    // Keep waiting while the stream is making real media progress (slow burn-in).
                    if (HasPlaybackStartProgress(lastBuffered))
                    {
                        lastBuffered = Math.Max(lastBuffered, PlayerService.BufferedTime);
                        deadline = DateTime.UtcNow + roundTimeout;
                    }

                    // Poll JS in case event interop missed buffer/time updates.
                    if (_isInitialized && !string.IsNullOrEmpty(_player.Id))
                    {
                        try
                        {
                            var buffered = await GetBufferedTimeAsync();
                            if (IsFinitePositive(buffered) && buffered > PlayerService.BufferedTime)
                                PlayerService.BufferedTime = buffered;

                            var currentTime = await GetCurrentTimeAsync();
                            if (IsFinitePositive(currentTime) && currentTime > PlayerService.CurrentTime)
                                PlayerService.CurrentTime = currentTime;

                            // Buffer without Playing often means autoplay was blocked; nudge play().
                            if (IsFinitePositive(PlayerService.BufferedTime)
                                && PlayerService.PlaybackState is not PlaybackState.Playing)
                            {
                                await JSRuntime.InvokeVoidAsync("play", _player.Id);
                            }
                        }
                        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException)
                        {
                        }
                    }

                    if (IsPlaybackReady())
                        return;

                    await Task.Delay(500, cancellationToken);
                }

                if (IsPlaybackReady() || !PlayerService.IsVisible)
                    return;

                if (isWindowsWebPlayer && round < maxRounds - 1)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[K7-Player] WaitForDurationReadyAsync idle timeout round="
                        + round
                        + " after "
                        + roundTimeout.TotalSeconds
                        + "s, attempting recovery");
                    var recovered = await PlayerService.TryRecoverPlaybackStartAsync(
                        allowQualityLadder: true,
                        cancellationToken: cancellationToken);
                    if (recovered)
                        continue;

                    break;
                }

                break;
            }

            if (IsPlaybackReady() || !PlayerService.IsVisible)
                return;

            System.Diagnostics.Debug.WriteLine("[K7-Player] WaitForDurationReadyAsync timed out, aborting playback start");
            await PlayerService.AbortPlaybackStartAsync(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void OnPlaybackStartFailed() => OnPlaybackStartFailedAsync().FireAndForget();

    private async Task OnPlaybackStartFailedAsync()
    {
        _durationWaitCts?.Cancel();

        try
        {
            await InvokeAsync(ShowPlaybackStartFailedSnackbarAsync);
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private Task ShowPlaybackStartFailedSnackbarAsync()
    {
        var messageKey = PlayerService.PlaybackStartFailureMessageKey
            ?? (PlayerService.Source?.StreamSessionId is not null
                ? "StreamPlaybackTimedOut"
                : "StreamNotReady");

        Snackbar.Add(S[messageKey], K7Severity.Error);
        return Task.CompletedTask;
    }

    private void OnSwitchAudioTrack(string trackName) => OnSwitchAudioTrackAsync(trackName).FireAndForget();

    private async Task OnSwitchAudioTrackAsync(string trackName)
    {
        if (_isInitialized && !string.IsNullOrEmpty(_player.Id))
        {
            await JSRuntime.InvokeVoidAsync("switchAudioTrack", _player.Id, trackName);
        }
    }

    private void OnSwitchSubtitleTrack(string? slug) => OnSwitchSubtitleTrackAsync(slug).FireAndForget();

    private async Task OnSwitchSubtitleTrackAsync(string? slug)
    {
        if (_isInitialized && !string.IsNullOrEmpty(_player.Id))
        {
            await JSRuntime.InvokeVoidAsync("switchSubtitleTrack", _player.Id, slug);
        }
    }

    private void OnAspectRatioModeChange(AspectRatioMode mode) => OnAspectRatioModeChangeAsync(mode).FireAndForget();

    private async Task OnAspectRatioModeChangeAsync(AspectRatioMode mode)
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
    public async Task SeekAsync(double seconds)
    {
        if (_isInitialized && !string.IsNullOrEmpty(_player.Id))
            await JSRuntime.InvokeVoidAsync("seek", _player.Id, seconds);
    }
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
                PlayerService.PlaybackState = PlaybackState.Buffering;
                break;

            // Triggered when a Component is ready.
            case "ready":
                break;

            // Triggered whenever a play event happens. Indicates that playback has started or resumed
            case "play":
                PlayerService.PlaybackState = PlaybackState.Buffering;
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
                _mediaCanPlay = true;
                break;

            // The media has a readyState of HAVE_ENOUGH_DATA or greater. This means that the entire media file can be played without buffering.
            case "canplaythrough":
                _mediaCanPlay = true;
                break;
        }
    }

    [JSInvokable]
    public void OnPlayerError(int code, string message)
    {
        // Soft errors (network/decode) are common while the server produces segment 0 for
        // burn-in. Only hard SRC_NOT_SUPPORTED may step quality, and only after cooldown.
        System.Diagnostics.Debug.WriteLine(
            "[K7-Player] Video.js error code=" + code + " message=" + message);

        // Never recover while media is already demuxing - reloads cause the blink loop.
        if (code != MediaErrSrcNotSupported
            || !PlayerService.IsVisible
            || IsPlaybackReady()
            || IsFinitePositive(PlayerService.BufferedTime))
            return;

        ReportHardVideoJsErrorToServer(code, message);
        OnHardPlayerErrorAsync().FireAndForget();
    }

    private void ReportHardVideoJsErrorToServer(int code, string message)
    {
        try
        {
            var source = PlayerService.Source;
            var sessionId = source?.StreamSessionId?.ToString() ?? "(none)";
            var quality = PlayerService.SelectedQuality?.Label ?? "(none)";
            var dedupeKey = code + "|" + message + "|" + sessionId + "|" + quality;
            var now = DateTime.UtcNow;
            if (_lastHardPlayerErrorReportKey == dedupeKey
                && now - _lastHardPlayerErrorReportUtc < HardPlayerErrorReportDedupeWindow)
            {
                return;
            }

            _lastHardPlayerErrorReportKey = dedupeKey;
            _lastHardPlayerErrorReportUtc = now;

            var reportMessage =
                "Video.js hard error code="
                + code
                + " message="
                + message
                + " StreamSessionId="
                + sessionId
                + " IndexedFileId="
                + (source?.IndexedFileId?.ToString() ?? "(none)")
                + " quality="
                + quality
                + " Position="
                + PlayerService.CurrentTime.ToString("F2")
                + "s Duration="
                + PlayerService.Duration.ToString("F2")
                + "s UsesWebVideoPlayer="
                + WindowsVideoPlayback.UsesWebVideoPlayer;

            ClientErrorReporter.ReportError(
                new InvalidOperationException(reportMessage),
                "VideoPlayer.OnPlayerError",
                notifyUser: false);
        }
        catch
        {
            // Best-effort reporting.
        }
    }

    private async Task OnHardPlayerErrorAsync()
    {
        try
        {
            var recovered = await PlayerService.TryRecoverPlaybackStartAsync(allowQualityLadder: true);
            if (!recovered)
                await PlayerService.AbortPlaybackStartAsync();
        }
        catch (ObjectDisposedException)
        {
        }
        catch (OperationCanceledException)
        {
        }
    }

    [JSInvokable]
    public void OnDurationChanged(double? duration)
    {
        if (duration is { } value && IsFinitePositive(value))
            PlayerService.Duration = value;
    }

    [JSInvokable]
    public void OnTimeUpdated(double? time)
    {
        if (time is { } value && !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0)
            PlayerService.CurrentTime = value;
    }

    [JSInvokable]
    public void OnBufferedUpdated(double? time)
    {
        if (time is { } value && IsFinitePositive(value))
            PlayerService.BufferedTime = value;
    }

    [JSInvokable]
    public void OnVolumeChanged(double? volume, bool muted)
    {
        if (volume.HasValue)
            PlayerService.Volume = volume.Value;
        PlayerService.IsMuted = muted;
    }

    [JSInvokable]
    public void OnPlaybackRateChanged(double? rate)
    {
        if (rate.HasValue)
            PlayerService.PlaybackRate = rate.Value;
    }

    [JSInvokable]
    public void OnFullscreenChanged(bool isFullscreen) => PlayerService.IsFullScreen = isFullscreen;
}
