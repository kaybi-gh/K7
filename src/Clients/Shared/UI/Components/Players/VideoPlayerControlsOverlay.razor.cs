using System.Globalization;
using System.Timers;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class VideoPlayerControlsOverlay : IAsyncDisposable
{
    [Parameter] public string? PosterImage { get; set; }
    [Parameter] public string? ThumbnailsSource { get; set; }
    [Parameter] public int ThumbnailsRows { get; set; } = 5;
    [Parameter] public int ThumbnailsCols { get; set; } = 5;
    [Parameter] public EventCallback<PlayerState> OnStateChange { get; set; }
    [Parameter] public EventCallback OnSyncPlayToggle { get; set; }
    [Parameter] public bool SyncPlaySidebarOpen { get; set; }
    [Parameter] public ElementReference ContainerRef { get; set; }

    private bool _showChapterTicks = true;
    private IReadOnlyList<MediaSegmentDto>? _mediaSegments;
    private Guid? _segmentsMediaId;
    private DeviceType _deviceType;
    private bool _showOverlay = true;
    private bool _isMenuOpen = false;
    private PlaybackSettingsMenu? _playbackSettingsMenu;
    private bool _isMouseOverControlsBar = false;

    private bool IsOverlayVisible => _showOverlay || _isMenuOpen || _isMouseOverVolumeSlider;
    private bool _isMouseOverVolumeButton = false;
    private bool _isMouseOverVolumeSlider = false;
    private bool _isVolumeSliderVisible = false;
    private System.Timers.Timer? _overlayVisibleTimer;
    private System.Timers.Timer? _seekDebounceTimer;
    private System.Timers.Timer? _hudTimer;
    private double _seekTarget;
    private double _seekOffset;
    private double _seekBaseTime;
    private bool _isSeeking;
    private string? _hudText;
    private double _hudScale = 1;
    private string _hudIcon = Phosphor.FastForward;
    private DateTime _lastSeekHudRenderUtc;
    private ElementReference _overlayRef;
    private SkipSegmentOverlay? _skipOverlay;
    private DotNetObjectReference<VideoPlayerControlsOverlay>? _dotNetRef;
    private static readonly TimeSpan _overlayTimeoutDesktop = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan _overlayTimeoutTv = TimeSpan.FromSeconds(5);
    private CancellationTokenSource? _volumePopoverHideDelayCts;

    // Touch gesture state
    private double _touchStartX;
    private double _touchStartY;
    private double _touchStartTime;
    private double _lastTapTime;
    private double _lastTapX;
    private bool _swipeGestureActive;
    private SwipeSide _swipeSide;
    private double _swipeBarPercent;
    private double _brightnessOverlayOpacity;
    private string? _doubleTapSide;
    private System.Timers.Timer? _doubleTapTimer;
    private System.Timers.Timer? _tapDelayTimer;
    private bool _tapPending;
    private double _viewportWidth;
    private const double SwipeThreshold = 15;
    private DotNetObjectReference<LayerCloseCallback>? _overlayCloseRef;
    private DateTime _suppressOverlayShowUntil;
    private bool _wasSidebarOpen;
    private bool _spatialNavInitialized;
    private bool _pendingLayerSync = true;
    private bool _needsRender = true;
    private DateTime _lastProgressRenderUtc;
    private volatile bool _disposed;
    private bool _isSeekBarScrubbing;
    private DateTime _suppressPlayerCloseUntil = DateTime.MinValue;

    private enum SwipeSide { Left, Right }

    private void OnPlaybackStateChanged(PlaybackState state) => RequestRender();
    private void OnIsMutedChanged(bool isMuted) => RequestRender();
    private void OnVolumeChanged(double volume) => RequestRender();
    private void OnCurrentTimeChanged(double time) => RequestProgressRender();
    private void OnBufferedTimeChanged(double time) => RequestProgressRender();
    private void OnPlaybackRateChanged(double rate) => RequestRender();
    private void OnIsFullScreenChanged(bool isFullScreen) => RequestRender();
    private void OnAudioTrackChanged(AudioFileTrackDto? track) => RequestRender();
    private void OnSubtitleTrackChanged(SubtitleFileTrackDto? track) => RequestRender();
    private void OnQualityChanged(VideoQualityOption? quality) => RequestRender();
    private void OnAspectRatioModeChanged(AspectRatioMode mode) => RequestRender();

    protected override bool ShouldRender()
    {
        if (!_needsRender)
            return false;

        _needsRender = false;
        return true;
    }

    private void RequestProgressRender()
    {
        if (DateTime.UtcNow - _lastProgressRenderUtc < TimeSpan.FromMilliseconds(250))
            return;

        _lastProgressRenderUtc = DateTime.UtcNow;
        RequestRender();
    }

    private void RequestRender()
    {
        if (_disposed)
            return;

        _needsRender = true;
        _ = InvokeAsync(StateHasChanged);
    }

    protected override async Task OnInitializedAsync()
    {
        _deviceType = await DeviceService.GetDeviceTypeAsync();
        try
        {
            var settings = await UserPreferencesService.GetEffectiveVideoPlayerSettingsAsync();
            PlayerService.ApplyVideoPlayerUxSettings(settings);
            _showChapterTicks = settings.ShowChapterTicks;
            await SubtitleStyleApplicator.ApplyAsync(JSRuntime, settings, _deviceType);
        }
        catch
        {
            _showChapterTicks = true;
        }

        var initialTimeout = _deviceType == DeviceType.TV ? _overlayTimeoutTv : _overlayTimeoutDesktop;
        _overlayVisibleTimer = new System.Timers.Timer(initialTimeout) { AutoReset = false };
        _overlayVisibleTimer.Elapsed += OnOverlayTimerElapsed;
        if (_deviceType is not (DeviceType.Phone or DeviceType.Tablet))
        {
            _overlayVisibleTimer.Start();
        }
        // Idle fallback only: keyboard seek commits on keyup. Interval must stay above typical
        // OS key-repeat initial delay (~250-500ms) so we never Seek mid-hold.
        _seekDebounceTimer = new System.Timers.Timer(1000) { AutoReset = false };
        _seekDebounceTimer.Elapsed += OnSeekDebounceElapsed;
        _hudTimer = new System.Timers.Timer(800) { AutoReset = false };
        _hudTimer.Elapsed += OnHudTimerElapsed;
        _doubleTapTimer = new System.Timers.Timer(500) { AutoReset = false };
        _doubleTapTimer.Elapsed += OnDoubleTapTimerElapsed;
        _tapDelayTimer = new System.Timers.Timer(300) { AutoReset = false };
        _tapDelayTimer.Elapsed += OnTapDelayElapsed;
        PlayerService.PlaybackStateChanged += OnPlaybackStateChanged;
        PlayerService.IsMutedChanged += OnIsMutedChanged;
        PlayerService.VolumeChanged += OnVolumeChanged;
        PlayerService.CurrentTimeChanged += OnCurrentTimeChanged;
        PlayerService.BufferedTimeChanged += OnBufferedTimeChanged;
        PlayerService.PlaybackRateChanged += OnPlaybackRateChanged;
        PlayerService.IsFullScreenChanged += OnIsFullScreenChanged;
        PlayerService.AudioTrackChanged += OnAudioTrackChanged;
        PlayerService.SubtitleTrackChanged += OnSubtitleTrackChanged;
        PlayerService.QualityChanged += OnQualityChanged;
        PlayerService.AspectRatioModeChanged += OnAspectRatioModeChanged;
        PlayerService.BackPressed += OnBackPressed;
        PlayerService.SourceChanged += OnSourceChanged;
        PlayerService.PlayerUxSettingsChanged += OnVideoPlayerUxSettingsChanged;
        await EnsureMediaSegmentsLoadedAsync(PlayerService.Source?.MediaId);
        if (DeviceService.GetClientType() == ClientType.Web)
        {
            await JSRuntime.InvokeVoidAsync("hideBodyScroll", true);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        _dotNetRef ??= DotNetObjectReference.Create(this);
        _overlayCloseRef ??= DotNetObjectReference.Create(new LayerCloseCallback(HandleBack));

        try
        {
            // Avoid re-registering / re-pushing the SpatialNav layer on every progress render
            // (~250ms). That flood makes TV D-pad feel laggy and fights seekbar edit focus.
            var sidebarChanged = SyncPlaySidebarOpen != _wasSidebarOpen;
            if (!_spatialNavInitialized)
            {
                await JSRuntime.InvokeVoidAsync("SpatialNav.registerVideoPlayerBack", _overlayCloseRef);
                await JSRuntime.InvokeVoidAsync("SpatialNav.registerVideoPlayerRemote", _dotNetRef);
                await SyncTvSkipSecondsToJsAsync();
                _spatialNavInitialized = true;
                _pendingLayerSync = true;
            }

            if (sidebarChanged)
            {
                _wasSidebarOpen = SyncPlaySidebarOpen;
                if (SyncPlaySidebarOpen)
                    await SpatialNav.PopLayerAsync(_overlayRef);
                else
                    await SpatialNav.PopLayerAsync(ContainerRef);
                _pendingLayerSync = true;
            }

            if (!_pendingLayerSync)
                return;

            _pendingLayerSync = false;
            var activeLayer = SyncPlaySidebarOpen ? ContainerRef : _overlayRef;
            await SpatialNav.PushLayerAsync(activeLayer, "overlay", new SpatialNavLayerOptions
            {
                OnClose = _overlayCloseRef,
                FocusSelector = ".play-pause-btn"
            });
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException) { }
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        // preventDefault for arrows is handled in JS capture phase (navigation.js)
        // to avoid Blazor render-cycle lag blocking Enter click synthesis on MAUI.
        // Android TV WebView sends empty Code for remote keys; fall back to Key.
        var code = string.IsNullOrEmpty(e.Code) ? e.Key : e.Code;

        // Global shortcuts - always active
        switch (code)
        {
            case "Space" or " " or "MediaPlayPause" or "MediaPlay" or "MediaPause":
                TogglePlayPause();
                ResetOverlayTimeout();
                return;
            case "Escape" or "BrowserBack" or "GoBack":
                HandleBack();
                return;
            case "MediaStop":
                OnCloseButtonClick();
                return;
            case "MediaFastForward" or "MediaSkipForward":
                SkipByConfiguredDirection(1);
                return;
            case "MediaRewind" or "MediaSkipBackward":
                SkipByConfiguredDirection(-1);
                return;
            case "KeyM" or "m" or "M":
                ToggleIsMuted();
                ResetOverlayTimeout();
                return;
            case "KeyF" or "f" or "F":
                ToggleFullscreen();
                ResetOverlayTimeout();
                return;
        }

        // When menu is open, JS handles arrow/Enter navigation inside popovers.
        if (_isMenuOpen) return;

        if (!_showOverlay)
        {
            if (IsSelectKey(e))
            {
                if (_skipOverlay?.CanSkip == true)
                {
                    _skipOverlay.SkipSegment();
                    return;
                }

                ShowOverlay();
                return;
            }

            // Overlay hidden: arrows control playback
            switch (code)
            {
                case "ArrowLeft":
                    if (UsesSeekBarScrubOnArrow())
                        _ = BeginSeekBarScrubAsync(-1);
                    else
                    {
                        AccumulateSeek(-GetSkipBackSeconds());
                        RequestSeekHudRender();
                    }
                    return;
                case "ArrowRight":
                    if (UsesSeekBarScrubOnArrow())
                        _ = BeginSeekBarScrubAsync(1);
                    else
                    {
                        AccumulateSeek(GetSkipForwardSeconds());
                        RequestSeekHudRender();
                    }
                    return;
                case "ArrowUp":
                    AdjustVolume(0.1);
                    return;
                case "ArrowDown":
                    AdjustVolume(-0.1);
                    return;
            }
        }
        // When overlay is visible, JS handles arrow navigation between controls.
    }

    private void OnKeyUp(KeyboardEventArgs e)
    {
        // Phone/Tablet keyboard accumulate-seek commits on keyup. TV/Desktop uses seekbar edit.
        if (_showOverlay || _isMenuOpen || !_isSeeking || UsesSeekBarScrubOnArrow())
            return;

        var code = string.IsNullOrEmpty(e.Code) ? e.Key : e.Code;
        if (code is not ("ArrowLeft" or "ArrowRight"))
            return;

        CommitSeek();
        RequestSeekHudRender(force: true);
    }

    private bool UsesSeekBarScrubOnArrow() =>
        _deviceType is not (DeviceType.Phone or DeviceType.Tablet);

    private async Task BeginSeekBarScrubAsync(int direction)
    {
        if (_disposed || _isMenuOpen)
            return;

        _showOverlay = true;
        ResetOverlayTimeout(TimeSpan.FromSeconds(5));
        await InvokeAsync(StateHasChanged);

        try
        {
            await JSRuntime.InvokeVoidAsync("K7.beginSeekBarScrub", direction);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException) { }
    }

    private static bool IsSelectKey(KeyboardEventArgs e)
    {
        var code = string.IsNullOrEmpty(e.Code) ? e.Key : e.Code;
        return code is "Enter" or "NumpadEnter" or "Select" or "DpadCenter";
    }

    private void ShowOverlay()
    {
        _showOverlay = true;
        ResetOverlayTimeout(TimeSpan.FromSeconds(5));
        RequestRender();
        _ = FocusPlayPauseAsync();
    }

    private async Task FocusPlayPauseAsync()
    {
        try
        {
            // Prefer play/pause so DPAD can leave the control. Focusing the seekbar first
            // often left SpatialNav paused / edit-sticky on Android TV.
            await SpatialNav.FocusFirstAsync(".play-pause-btn");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException) { }
    }

    private void HideOverlay(bool syncDom = true)
    {
        _showOverlay = false;
        _isSeekBarScrubbing = false;
        _overlayVisibleTimer?.Stop();
        _suppressOverlayShowUntil = DateTime.UtcNow.AddMilliseconds(500);
        _isMouseOverControlsBar = false;
        if (syncDom)
            _ = SyncOverlayHiddenInDomAsync();
        _ = CancelSeekBarEditingAsync();
        _ = _overlayRef.FocusAsync();
    }

    private async Task SyncOverlayHiddenInDomAsync()
    {
        try
        {
            // JS may have forced controls-visible during scrub; sync DOM with Blazor state
            // so Escape does not "stop video while chrome stays painted".
            await JSRuntime.InvokeVoidAsync("K7.hideVideoControlsOverlay");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException) { }
    }

    private async Task CancelSeekBarEditingAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("SpatialNav.cancelEditingIn", ".video-controls-overlay");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException) { }
    }

    private async Task SoftCancelSeekBarEditingAsync()
    {
        try
        {
            await JSRuntime.InvokeAsync<string>("K7.cancelVideoSeekOrEdit");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException) { }
    }

    private bool ShouldIgnoreMouseOverlayShow() =>
        _deviceType == DeviceType.TV || DateTime.UtcNow < _suppressOverlayShowUntil;

    private void AccumulateSeek(double offset, bool startIdleCommit = true, bool showHud = true)
    {
        if (!_isSeeking)
        {
            _seekBaseTime = PlayerService.CurrentTime;
            _seekOffset = 0;
        }
        _seekOffset += offset;
        _seekTarget = Math.Clamp(_seekBaseTime + _seekOffset, 0, PlayerService.Duration);
        _isSeeking = true;

        if (showHud)
        {
            _hudIcon = _seekOffset >= 0 ? Phosphor.FastForward : Phosphor.Rewind;
            _hudScale = 1.0 + Math.Min(Math.Abs(_seekOffset) / 200.0, 0.35);
            ShowHud($"{(_seekOffset >= 0 ? "+" : "")}{(int)_seekOffset}s");
        }

        // Preview only while holding / tapping - Seek runs on keyup or idle end of burst.
        if (startIdleCommit)
        {
            _seekDebounceTimer?.Stop();
            _seekDebounceTimer?.Start();
        }
    }

    private void CommitSeek()
    {
        if (!_isSeeking)
            return;

        _seekDebounceTimer?.Stop();
        PlayerService.Seek(_seekTarget);
        _isSeeking = false;
        _seekOffset = 0;
    }

    private void RequestSeekHudRender(bool force = false)
    {
        if (!force && DateTime.UtcNow - _lastSeekHudRenderUtc < TimeSpan.FromMilliseconds(50))
            return;

        _lastSeekHudRenderUtc = DateTime.UtcNow;
        RequestRender();
    }

    [JSInvokable]
    public void OnRemoteSelect()
    {
        if (_disposed) return;
        if (_isMenuOpen) return;

        if (!_showOverlay)
        {
            ShowOverlay();
            RequestRender();
        }
    }

    /// <summary>Short-press skip when native seekBy already moved the player - HUD only.</summary>
    [JSInvokable]
    public void OnRemoteSkipHud(double deltaSeconds)
    {
        if (_disposed || _isMenuOpen) return;
        _hudIcon = deltaSeconds >= 0 ? Phosphor.FastForward : Phosphor.Rewind;
        _hudScale = 1.15;
        ShowHud($"{(deltaSeconds >= 0 ? "+" : "")}{(int)deltaSeconds}s");
        RequestSeekHudRender(force: true);
    }

    /// <summary>Short-press skip using configured SkipBack/SkipForward preferences (dir -1 or +1).</summary>
    [JSInvokable]
    public void OnRemoteSkipDirection(int direction)
    {
        if (_disposed || _isMenuOpen) return;
        if (_showOverlay) return;

        var delta = direction < 0 ? -GetSkipBackSeconds() : GetSkipForwardSeconds();
        OnRemoteSkipSeconds(delta);
    }

    /// <summary>Short-press +/- Ns when the native bridge is unavailable.</summary>
    [JSInvokable]
    public void OnRemoteSkipSeconds(double deltaSeconds)
    {
        if (_disposed || _isMenuOpen) return;
        if (_showOverlay) return;

        var target = Math.Clamp(
            PlayerService.CurrentTime + deltaSeconds,
            0,
            Math.Max(0, PlayerService.Duration));
        PlayerService.Seek(target);
        OnRemoteSkipHud(deltaSeconds);
    }

    private int GetSkipBackSeconds() =>
        Math.Max(1, PlayerService.SkipBackSeconds);

    private int GetSkipForwardSeconds() =>
        Math.Max(1, PlayerService.SkipForwardSeconds);

    private void SkipByConfiguredDirection(int direction)
    {
        var delta = direction < 0 ? -GetSkipBackSeconds() : GetSkipForwardSeconds();
        var target = Math.Clamp(
            PlayerService.CurrentTime + delta,
            0,
            Math.Max(0, PlayerService.Duration));
        PlayerService.Seek(target);
        OnRemoteSkipHud(delta);
        ResetOverlayTimeout();
    }

    private async Task SyncTvSkipSecondsToJsAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync(
                "K7.setTvSkipSeconds",
                GetSkipBackSeconds(),
                GetSkipForwardSeconds());
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException) { }
    }

    private void OnVideoPlayerUxSettingsChanged()
    {
        if (_disposed) return;
        _ = InvokeAsync(async () =>
        {
            await SyncTvSkipSecondsToJsAsync();
            await RefreshSubtitleStyleAsync();
        });
    }

    private async Task RefreshSubtitleStyleAsync()
    {
        try
        {
            var settings = PlayerService.VideoPlayerUxSettings
                ?? await UserPreferencesService.GetEffectiveVideoPlayerSettingsAsync();
            await SubtitleStyleApplicator.ApplyAsync(JSRuntime, settings, _deviceType);
        }
        catch
        {
        }
    }

    /// <summary>
    /// JS soft-cancelled seekbar edit (OK then Back, no scrub). Keep overlay visible.
    /// </summary>
    [JSInvokable]
    public void OnRemoteSeekEditCancelled()
    {
        if (_disposed) return;
        _isSeekBarScrubbing = false;
        ResetOverlayTimeout(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// JS already hid chrome (tvBack / scrub commit). Sync Blazor flags without another JS round-trip.
    /// </summary>
    [JSInvokable]
    public void OnRemoteOverlayHidden()
    {
        if (_disposed) return;

        _showOverlay = false;
        _isSeekBarScrubbing = false;
        _overlayVisibleTimer?.Stop();
        _suppressOverlayShowUntil = DateTime.UtcNow.AddMilliseconds(500);
        _isMouseOverControlsBar = false;
        _suppressPlayerCloseUntil = DateTime.UtcNow.AddMilliseconds(450);
        _ = InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// JS already scrubbed via K7.beginSeekBarScrub. Only sync Blazor chrome visibility
    /// (do not call beginSeekBarScrub again - that doubled work and flooded render batches).
    /// </summary>
    [JSInvokable]
    public void OnRemoteOverlayShown()
    {
        if (_disposed || _isMenuOpen)
            return;

        if (_showOverlay)
        {
            ResetOverlayTimeout(TimeSpan.FromSeconds(5));
            return;
        }

        _showOverlay = true;
        ResetOverlayTimeout(TimeSpan.FromSeconds(5));
        _ = InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public void OnRemoteOpenSeekBarScrub(int direction)
    {
        if (_disposed || _isMenuOpen)
            return;

        if (!UsesSeekBarScrubOnArrow())
        {
            if (_showOverlay)
                return;

            AccumulateSeek(direction < 0 ? -GetSkipBackSeconds() : GetSkipForwardSeconds());
            RequestSeekHudRender(force: true);
            return;
        }

        // TV path: JS owns scrub stepping. Keep Blazor overlay flags in sync only.
        OnRemoteOverlayShown();
    }

    [JSInvokable]
    public void OnRemoteSeekCommit()
    {
        if (_disposed) return;
        if (_showOverlay || _isMenuOpen || UsesSeekBarScrubOnArrow()) return;
        CommitSeek();
        RequestSeekHudRender(force: true);
    }

    [JSInvokable]
    public void OnRemoteVolumeUp() => OnRemoteVolumeStep(0.1);

    [JSInvokable]
    public void OnRemoteVolumeDown() => OnRemoteVolumeStep(-0.1);

    [JSInvokable]
    public void OnRemoteVolumeStep(double delta)
    {
        if (_disposed) return;
        if (_showOverlay || _isMenuOpen) return;
        AdjustVolume(delta);
        RequestRender();
    }

    private void AdjustVolume(double delta)
    {
        var newVolume = Math.Clamp(PlayerService.Volume + delta, 0, 1);
        PlayerService.SetVolume(newVolume);
        _hudIcon = delta > 0 ? Phosphor.SpeakerHigh : Phosphor.SpeakerLow;
        ShowHud($"{(int)Math.Round(newVolume * 100)}%");
    }

    private void ShowHud(string text)
    {
        _hudText = text;
        _hudTimer?.Stop();
        _hudTimer?.Start();
    }

    private void OnHudTimerElapsed(object? sender, ElapsedEventArgs args)
    {
        if (_disposed) return;

        InvokeAsync(() =>
        {
            _hudText = null;
            _hudScale = 1;
            RequestRender();
        });
    }

    private void OnOverlayTimerElapsed(object? sender, ElapsedEventArgs args)
        => OnOverlayTimerElapsedAsync().FireAndForget();

    private async Task OnOverlayTimerElapsedAsync()
    {
        if (_disposed) return;

        await InvokeAsync(async () =>
        {
            try
            {
                if (_isMenuOpen || _isSeeking) return;
                // Keep overlay visible while seekbar is being scrubbed (keyboard editing mode)
                var isEditing = await JSRuntime.InvokeAsync<bool>(
                    "SpatialNav.hasEditingIn", ".video-controls-overlay");
                if (isEditing)
                {
                    ResetOverlayTimeout();
                    return;
                }

                // Use HideOverlay so seekbar edit mode is cancelled (do not leave
                // data-sn-editing + orphan JS thumbnails while controls are hidden).
                HideOverlay();
                StateHasChanged();
                if (!SyncPlaySidebarOpen)
                    await _overlayRef.FocusAsync();
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException) { }
        });
    }

    private void OnSeekDebounceElapsed(object? sender, ElapsedEventArgs args)
    {
        if (_disposed) return;

        InvokeAsync(() =>
        {
            CommitSeek();
            RequestRender();
        });
    }

    [JSInvokable]
    public async Task CloseMenu()
    {
        await Task.Delay(1);
        _isMenuOpen = false;
        await InvokeAsync(StateHasChanged);
        await Task.Yield();
        if (!SyncPlaySidebarOpen)
            await _overlayRef.FocusAsync();
        ResetOverlayTimeout(TimeSpan.FromSeconds(5));
    }

    public void ResetOverlayTimeout(TimeSpan? timeout = null)
    {
        _showOverlay = true;
        _overlayVisibleTimer?.Stop();
        if (_deviceType is DeviceType.Phone or DeviceType.Tablet) return;
        if (timeout.HasValue && _overlayVisibleTimer is not null)
        {
            _overlayVisibleTimer.Interval = timeout.Value.TotalMilliseconds;
        }
        _overlayVisibleTimer?.Start();
    }

    private void OnCloseButtonClick()
    {
        HideOverlay();
        PlayerService.Stop();
        PlayerService.HideAsync();
    }

    private void PerformBackStep()
    {
        if (_isMenuOpen && _playbackSettingsMenu?.TryHandleBack() == true)
            return;

        if (_isVolumeSliderVisible || _isMouseOverVolumeSlider)
        {
            _isVolumeSliderVisible = false;
            _isMouseOverVolumeSlider = false;
            return;
        }

        // Seekbar edit without L/R scrub: cancel via JS soft path in HandleBack.
        // Do not HideOverlay here - that was quitting playback when a second Back raced.
        if (_isSeekBarScrubbing)
        {
            _isSeekBarScrubbing = false;
            _ = SoftCancelSeekBarEditingAsync();
            return;
        }

        if (_showOverlay)
        {
            HideOverlay();
            // Swallow a second Back that races from native + Blazor on the same press.
            _suppressPlayerCloseUntil = DateTime.UtcNow.AddMilliseconds(450);
            return;
        }

        if (DateTime.UtcNow < _suppressPlayerCloseUntil)
            return;

        // Empty/stuck overlay after Stop: still allow leaving the player shell.
        if (PlayerService.PlaybackState is PlaybackState.Idle
            or PlaybackState.Ended
            or PlaybackState.Unknown)
        {
            HideOverlay();
            PlayerService.HideAsync();
            return;
        }

        OnCloseButtonClick();
    }

    private void HandleBack()
    {
        if (_disposed) return;

        // Observe the task: an unobserved exception here becomes #blazor-error-ui
        // (ErrorBoundary does not cover event/dispose continuations).
        _ = InvokeAsync(async () =>
        {
            try
            {
                try
                {
                    var cancelResult = await JSRuntime.InvokeAsync<string>("K7.cancelVideoSeekOrEdit");
                    if (cancelResult == "soft")
                    {
                        // OK then Escape: leave edit mode, keep chrome / playback.
                        _isSeekBarScrubbing = false;
                        await ReattachLayerCallbackAsync();
                        if (!_disposed)
                            StateHasChanged();
                        return;
                    }

                    if (cancelResult == "hard")
                    {
                        // Scrub cancel: DotNet OnEditCancel already hides via OnDragChanged.
                        _isSeekBarScrubbing = false;
                        _suppressPlayerCloseUntil = DateTime.UtcNow.AddMilliseconds(450);
                        await ReattachLayerCallbackAsync();
                        if (!_disposed)
                            StateHasChanged();
                        return;
                    }
                }
                catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException)
                {
                }

                PerformBackStep();

                // Close tears down this overlay - do not reattach / re-render a disposed tree.
                if (_disposed || !PlayerService.IsVisible)
                    return;

                await ReattachLayerCallbackAsync();
                if (!_disposed)
                    StateHasChanged();
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException)
            {
            }
        });
    }

    private async Task ReattachLayerCallbackAsync()
    {
        if (_disposed || _overlayCloseRef is null)
            return;

        try
        {
            var activeLayer = SyncPlaySidebarOpen ? ContainerRef : _overlayRef;
            await SpatialNav.AttachLayerCallbackAsync(activeLayer, _overlayCloseRef);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException) { }
    }

    private void OnBackPressed()
    {
        HandleBack();
    }

    private void OnFocusIn(FocusEventArgs args)
    {
        if (_showOverlay)
        {
            ResetOverlayTimeout();
        }
    }

    private void OnOverlayTap()
    {
        if (_showOverlay && !_isMouseOverControlsBar && !_isMenuOpen)
        {
            _showOverlay = false;
            _overlayVisibleTimer?.Stop();
        }
        else
        {
            _showOverlay = true;
            ResetOverlayTimeout(TimeSpan.FromSeconds(5));
        }

        // ShouldRender is gated on _needsRender; without this, Blazor click never paints.
        RequestRender();
    }

    private void OnOverlayMouseMove(MouseEventArgs args)
    {
        if (ShouldIgnoreMouseOverlayShow() || _isMouseOverControlsBar || _isMenuOpen)
        {
            return;
        }

        if (_showOverlay)
        {
            ResetOverlayTimeout();
            return;
        }

        _showOverlay = true;
        ResetOverlayTimeout();
        RequestRender();
    }

    private async Task OnTouchStart(TouchEventArgs e)
    {
        if (e.Touches.Length != 1) return;
        var touch = e.Touches[0];
        _touchStartX = touch.ClientX;
        _touchStartY = touch.ClientY;
        _touchStartTime = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

        if (_viewportWidth <= 0)
        {
            _viewportWidth = await JSRuntime.InvokeAsync<double>("K7.getViewportWidth");
        }
    }

    private void OnTouchMove(TouchEventArgs e)
    {
        if (e.Touches.Length != 1 || _isMouseOverControlsBar || _isMenuOpen) return;
        var touch = e.Touches[0];
        var dx = Math.Abs(touch.ClientX - _touchStartX);
        var dy = touch.ClientY - _touchStartY;

        if (!_swipeGestureActive && (dx < SwipeThreshold && Math.Abs(dy) > SwipeThreshold))
        {
            _swipeGestureActive = true;
            _swipeSide = _touchStartX < _viewportWidth / 2 ? SwipeSide.Left : SwipeSide.Right;
        }

        if (_swipeGestureActive)
        {
            var delta = -dy / 300.0;
            if (_swipeSide == SwipeSide.Right)
            {
                if (VolumeService.SupportsNativeVolume)
                {
                    var newVolume = Math.Clamp(VolumeService.Volume + delta, 0, 1);
                    VolumeService.SetVolume(newVolume);
                    _hudIcon = Phosphor.SpeakerHigh;
                    _hudText = $"{(int)Math.Round(newVolume * 100)}%";
                    _swipeBarPercent = VolumeService.Volume * 100;
                }
                else
                {
                    var newVolume = Math.Clamp(PlayerService.Volume + delta, 0, 1);
                    PlayerService.SetVolume(newVolume);
                    _hudIcon = Phosphor.SpeakerHigh;
                    _hudText = $"{(int)Math.Round(newVolume * 100)}%";
                    _swipeBarPercent = newVolume * 100;
                }
            }
            else
            {
                var newBrightness = Math.Clamp(BrightnessService.Brightness + delta, 0, 1);
                BrightnessService.SetBrightness(newBrightness);
                if (!BrightnessService.SupportsNativeBrightness)
                {
                    _brightnessOverlayOpacity = 1.0 - newBrightness;
                }
                _hudIcon = Phosphor.Sun;
                _hudText = $"{(int)Math.Round(newBrightness * 100)}%";
                _swipeBarPercent = newBrightness * 100;
            }

            _touchStartY = touch.ClientY;
            StateHasChanged();
        }
    }

    private void OnTouchEnd(TouchEventArgs e)
    {
        if (_swipeGestureActive)
        {
            _swipeGestureActive = false;
            _hudText = null;
            StateHasChanged();
            return;
        }

        // Reset mouse-over flag on touch - Android WebView can emit stale mouseover events
        _isMouseOverControlsBar = false;

        if (_isMenuOpen) return;

        var now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        var elapsed = now - _touchStartTime;
        if (elapsed > 500) return;

        var isRightHalf = _touchStartX > _viewportWidth / 2;

        if (now - _lastTapTime < 300 && IsSameSide(_lastTapX, _touchStartX))
        {
            _tapDelayTimer?.Stop();
            _tapPending = false;
            HandleDoubleTap(isRightHalf);
        }
        else
        {
            _lastTapTime = now;
            _lastTapX = _touchStartX;
            _tapPending = true;
            _tapDelayTimer?.Stop();
            _tapDelayTimer?.Start();
        }
    }

    private bool IsSameSide(double x1, double x2)
    {
        var mid = _viewportWidth / 2;
        return (x1 < mid) == (x2 < mid);
    }

    private void HandleDoubleTap(bool isRightHalf)
    {
        var seekStep = isRightHalf ? GetSkipForwardSeconds() : -GetSkipBackSeconds();
        _doubleTapSide = isRightHalf ? "right" : "left";

        // Same model as keyboard hold: accumulate offset, seek once when the burst ends.
        AccumulateSeek(seekStep, startIdleCommit: false);

        _doubleTapTimer?.Stop();
        _doubleTapTimer?.Start();
        RequestSeekHudRender(force: true);
    }

    private void OnDoubleTapTimerElapsed(object? sender, ElapsedEventArgs args)
    {
        if (_disposed) return;

        InvokeAsync(() =>
        {
            CommitSeek();
            _doubleTapSide = null;
            RequestRender();
        });
    }

    private void OnTapDelayElapsed(object? sender, ElapsedEventArgs args)
    {
        if (_disposed) return;

        InvokeAsync(() =>
        {
            if (_tapPending)
            {
                _tapPending = false;
                OnOverlayTap();
                StateHasChanged();
            }
        });
    }

    private void OnSeekBarDragChanged(bool dragging)
    {
        if (dragging)
        {
            _isSeekBarScrubbing = true;
            _showOverlay = true;
            ResetOverlayTimeout();
            RequestRender();
            return;
        }

        var wasScrubbing = _isSeekBarScrubbing;
        _isSeekBarScrubbing = false;

        // After keyboard scrub seek/cancel, dismiss chrome. DOM is already synced by
        // K7.SeekBar.afterScrubCommit from OnEditCommitAt - avoid a second JS round-trip
        // that can stall the Blazor dispatcher on Android TV.
        if (wasScrubbing)
        {
            HideOverlay(syncDom: false);
            _suppressPlayerCloseUntil = DateTime.UtcNow.AddMilliseconds(450);
            RequestRender();
        }
    }

    private void OnSourceChanged(PlayerSource source)
    {
        RequestRender();
        _ = EnsureMediaSegmentsLoadedAsync(source.MediaId);
    }

    private async Task EnsureMediaSegmentsLoadedAsync(Guid? mediaId)
    {
        if (mediaId == _segmentsMediaId)
            return;

        _segmentsMediaId = mediaId;
        _mediaSegments = null;

        if (mediaId is null)
        {
            RequestRender();
            return;
        }

        try
        {
            _mediaSegments = await MediaService.GetMediaSegmentsAsync(mediaId.Value);
        }
        catch
        {
            _mediaSegments = null;
        }

        RequestRender();
    }

    private List<SeekBar.Chapter> GetSeekBarChapters()
    {
        var markers = SeekBarChapterBuilder.Build(
            _showChapterTicks,
            PlayerService.Source?.Chapters,
            _mediaSegments,
            S["Intro"],
            S["Outro"]);

        return markers
            .Select(m => new SeekBar.Chapter { Title = m.Title, Start = m.StartSeconds })
            .ToList();
    }

    private void OnControlsBarMouseEnter(MouseEventArgs args)
    {
        if (ShouldIgnoreMouseOverlayShow())
            return;

        _overlayVisibleTimer?.Stop();
        _isMouseOverControlsBar = true;
        if (!_showOverlay)
        {
            _showOverlay = true;
            RequestRender();
        }
        else
        {
            _showOverlay = true;
        }
    }

    private void OnControlsBarMouseOut(MouseEventArgs args)
    {
        if (ShouldIgnoreMouseOverlayShow())
            return;

        _isMouseOverControlsBar = false;
        _showOverlay = true;
        _overlayVisibleTimer?.Start();
    }

    private void OnVolumeButtonMouseOver(MouseEventArgs args)
    {
        _isMouseOverVolumeButton = true;
        _isVolumeSliderVisible = true;
    }

    private void OnVolumeButtonMouseOut(MouseEventArgs args)
    {
        _isMouseOverVolumeButton = false;
        HidePopover();
    }

    private void OnVolumeSliderMouseOver(MouseEventArgs args)
    {
        _isMouseOverVolumeSlider = true;
        _isVolumeSliderVisible = true;
    }

    private void OnVolumeSliderMouseOut(MouseEventArgs args)
    {
        _isMouseOverVolumeSlider = false;
        HidePopover();
    }

    private void HidePopover() => HidePopoverAsync().FireAndForget();

    private async Task HidePopoverAsync()
    {
        _volumePopoverHideDelayCts?.Cancel();
        _volumePopoverHideDelayCts = new CancellationTokenSource();
        try
        {
            await Task.Delay(500, _volumePopoverHideDelayCts.Token);
            if (_disposed) return;
            if (!_isMouseOverVolumeButton && !_isMouseOverVolumeSlider)
            {
                _isVolumeSliderVisible = false;
                StateHasChanged();
            }
        }
        catch (TaskCanceledException) { }
    }

    private void TogglePlayPause()
    {
        if (PlayerService.PlaybackState != PlaybackState.Playing)
        {
            PlayerService.Play();
        }
        else
        {
            PlayerService.Pause();
        }
    }

    private void ToggleIsMuted()
    {
        if (PlayerService.IsMuted)
        {
            PlayerService.IsMuted = false;
            PlayerService.Unmute();
        }
        else
        {
            PlayerService.IsMuted = true;
            PlayerService.Mute();
        }
    }

    private void ToggleFullscreen()
    {
        if (!PlayerService.IsFullScreen)
        {
            PlayerService.IsFullScreen = true;
            PlayerService.EnterFullScreen();
        }
        else
        {
            PlayerService.IsFullScreen = false;
            PlayerService.ExitFullScreen();
        }
    }

    private string FormatTime(double seconds)
    {
        var timeSpan = TimeSpan.FromSeconds(seconds);
        return timeSpan.Hours > 0
        ? $"{timeSpan.Hours:0}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}"
        : $"{timeSpan.Minutes:0}:{timeSpan.Seconds:00}";
    }

    private void HandleVolumeInput(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var volume))
        {
            PlayerService.SetVolume(volume);
        }
    }

    private string GetVolumeIcon()
    {
        if (PlayerService.IsMuted)
        {
            return Phosphor.SpeakerX;
        }

        return PlayerService.Volume switch
        {
            0 => Phosphor.SpeakerNone,
            < 0.5d => Phosphor.SpeakerLow,
            >= 0.5d => Phosphor.SpeakerHigh,
            _ => Phosphor.SpeakerNone
        };
    }

    public class PlayerState
    {
        public double CurrentTime { get; set; }
        public double Duration { get; set; }
        public bool IsPlaying { get; set; }
        public bool IsMuted { get; set; }
        public int Volume { get; set; }
        public bool IsFullscreen { get; set; }
    }

    private async Task OnCastDeviceSelected(CastDeviceInfo device)
    {
        await CastOrchestration.CastCurrentVideoAsync(device);
    }

    private async Task OnRemoteDeviceSelected(ConnectedDeviceDto device)
    {
        var source = PlayerService.Source;
        if (source?.IndexedFileId is null) return;

        PlayerService.Pause();

        var senderDeviceId = DeviceStorage.Get(PreferenceKeys.DEVICE_ID);
        var request = new K7.Shared.Dtos.RemotePlaybackRequestDto
        {
            IndexedFileId = source.IndexedFileId.Value,
            StartPosition = PlayerService.CurrentTime,
            IsAudio = false,
            Title = source.Title,
            CoverUrl = source.CoverUrl,
            Duration = PlayerService.Duration,
            SenderDeviceId = senderDeviceId is not null ? Guid.Parse(senderDeviceId.AsSpan()) : null
        };

        await HubClient.RequestRemotePlaybackAsync(device.DeviceId, request);
        RemoteControl.StartSession(device.DeviceId, device.DeviceName, request);
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _volumePopoverHideDelayCts?.Cancel();
        _volumePopoverHideDelayCts?.Dispose();
        _overlayVisibleTimer?.Dispose();
        _seekDebounceTimer?.Dispose();
        _hudTimer?.Dispose();
        _doubleTapTimer?.Dispose();
        _tapDelayTimer?.Dispose();
        BrightnessService.ResetBrightness();
        try
        {
            await JSRuntime.InvokeVoidAsync("SpatialNav.unregisterVideoPlayerBack");
            await JSRuntime.InvokeVoidAsync("SpatialNav.unregisterVideoPlayerRemote");
            await JSRuntime.InvokeVoidAsync("K7.setNativePlayerActive", false);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException) { }

        try
        {
            await SpatialNav.PopLayerAsync(_overlayRef);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException) { }

        try
        {
            await SpatialNav.PopLayerAsync(ContainerRef);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException) { }
        _dotNetRef?.Dispose();
        _overlayCloseRef?.Dispose();
        PlayerService.PlaybackStateChanged -= OnPlaybackStateChanged;
        PlayerService.IsMutedChanged -= OnIsMutedChanged;
        PlayerService.VolumeChanged -= OnVolumeChanged;
        PlayerService.CurrentTimeChanged -= OnCurrentTimeChanged;
        PlayerService.BufferedTimeChanged -= OnBufferedTimeChanged;
        PlayerService.PlaybackRateChanged -= OnPlaybackRateChanged;
        PlayerService.IsFullScreenChanged -= OnIsFullScreenChanged;
        PlayerService.AudioTrackChanged -= OnAudioTrackChanged;
        PlayerService.SubtitleTrackChanged -= OnSubtitleTrackChanged;
        PlayerService.QualityChanged -= OnQualityChanged;
        PlayerService.AspectRatioModeChanged -= OnAspectRatioModeChanged;
        PlayerService.BackPressed -= OnBackPressed;
        PlayerService.SourceChanged -= OnSourceChanged;
        PlayerService.PlayerUxSettingsChanged -= OnVideoPlayerUxSettingsChanged;
        if (DeviceService.GetClientType() == ClientType.Web)
        {
            await JSRuntime.InvokeVoidAsync("hideBodyScroll", false);
        }
    }
}
