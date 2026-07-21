using System.Globalization;
using System.Timers;
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
    private string _hudIcon = Phosphor.FastForward;
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
    private int _doubleTapSeekCount;
    private double _viewportWidth;
    private const double SwipeThreshold = 15;
    private DotNetObjectReference<LayerCloseCallback>? _overlayCloseRef;
    private DateTime _suppressOverlayShowUntil;
    private bool _wasSidebarOpen;
    private bool _needsRender = true;
    private DateTime _lastProgressRenderUtc;
    private volatile bool _disposed;

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
            _showChapterTicks = settings.ShowChapterTicks;
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
        _seekDebounceTimer = new System.Timers.Timer(300) { AutoReset = false };
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
            await JSRuntime.InvokeVoidAsync("SpatialNav.registerVideoPlayerBack", _overlayCloseRef);
            await JSRuntime.InvokeVoidAsync("SpatialNav.registerVideoPlayerRemote", _dotNetRef);

            if (SyncPlaySidebarOpen != _wasSidebarOpen)
            {
                _wasSidebarOpen = SyncPlaySidebarOpen;
                if (SyncPlaySidebarOpen)
                    await SpatialNav.PopLayerAsync(_overlayRef);
                else
                    await SpatialNav.PopLayerAsync(ContainerRef);
            }

            var activeLayer = SyncPlaySidebarOpen ? ContainerRef : _overlayRef;
            await SpatialNav.PushLayerAsync(activeLayer, "overlay", new SpatialNavLayerOptions
            {
                OnClose = _overlayCloseRef,
                FocusSelector = _deviceType == DeviceType.TV ? ".seekbar-container" : ".play-pause-btn"
            });
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
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
                System.Diagnostics.Debug.WriteLine("[K7-Player] overlay keydown: play/pause");
                TogglePlayPause();
                ResetOverlayTimeout();
                return;
            case "Escape" or "BrowserBack" or "GoBack":
                System.Diagnostics.Debug.WriteLine("[K7-Player] overlay keydown: escape/back");
                PerformBackStep();
                _ = ReattachLayerCallbackAsync();
                StateHasChanged();
                return;
            case "MediaStop":
                System.Diagnostics.Debug.WriteLine("[K7-Player] overlay keydown: media stop");
                OnCloseButtonClick();
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
                    AccumulateSeek(-10);
                    return;
                case "ArrowRight":
                    AccumulateSeek(10);
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

    private static bool IsSelectKey(KeyboardEventArgs e)
    {
        var code = string.IsNullOrEmpty(e.Code) ? e.Key : e.Code;
        return code is "Enter" or "NumpadEnter" or "Select" or "DpadCenter";
    }

    private void ShowOverlay()
    {
        _showOverlay = true;
        ResetOverlayTimeout(TimeSpan.FromSeconds(5));
        _ = FocusPlayPauseAsync();
    }

    private async Task FocusPlayPauseAsync()
    {
        try
        {
            var selector = _deviceType == DeviceType.TV ? ".seekbar-container" : ".play-pause-btn";
            await SpatialNav.FocusFirstAsync(selector);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
    }

    private void HideOverlay()
    {
        _showOverlay = false;
        _overlayVisibleTimer?.Stop();
        _suppressOverlayShowUntil = DateTime.UtcNow.AddMilliseconds(500);
        _isMouseOverControlsBar = false;
        _ = CancelSeekBarEditingAsync();
        _ = _overlayRef.FocusAsync();
    }

    private async Task CancelSeekBarEditingAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("SpatialNav.cancelEditingIn", ".video-controls-overlay");
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
    }

    private bool ShouldIgnoreMouseOverlayShow() =>
        _deviceType == DeviceType.TV || DateTime.UtcNow < _suppressOverlayShowUntil;

    private void AccumulateSeek(double offset)
    {
        if (!_isSeeking)
        {
            _seekBaseTime = PlayerService.CurrentTime;
            _seekOffset = 0;
        }
        _seekOffset += offset;
        _seekTarget = Math.Clamp(_seekBaseTime + _seekOffset, 0, PlayerService.Duration);
        _isSeeking = true;
        _hudIcon = _seekOffset >= 0 ? Phosphor.FastForward : Phosphor.Rewind;
        ShowHud($"{(_seekOffset >= 0 ? "+" : "")}{(int)_seekOffset}s");
        _seekDebounceTimer?.Stop();
        _seekDebounceTimer?.Start();
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

    [JSInvokable]
    public void OnRemoteSelect()
    {
        if (_disposed) return;

        InvokeAsync(() =>
        {
            if (_isMenuOpen) return;

            if (!_showOverlay)
            {
                ShowOverlay();
                StateHasChanged();
            }
        });
    }

    [JSInvokable]
    public void OnRemoteSeekLeft() => OnRemoteSeekAccumulate(-10);

    [JSInvokable]
    public void OnRemoteSeekRight() => OnRemoteSeekAccumulate(10);

    [JSInvokable]
    public void OnRemoteSeekAccumulate(double offset)
    {
        if (_disposed) return;

        InvokeAsync(() =>
        {
            if (_showOverlay || _isMenuOpen) return;
            AccumulateSeek(offset);
            StateHasChanged();
        });
    }

    [JSInvokable]
    public void OnRemoteSeekCommit()
    {
        if (_disposed) return;

        InvokeAsync(() =>
        {
            if (_showOverlay || _isMenuOpen) return;
            CommitSeek();
            StateHasChanged();
        });
    }

    [JSInvokable]
    public void OnRemoteVolumeUp() => OnRemoteVolumeStep(0.1);

    [JSInvokable]
    public void OnRemoteVolumeDown() => OnRemoteVolumeStep(-0.1);

    [JSInvokable]
    public void OnRemoteVolumeStep(double delta)
    {
        if (_disposed) return;

        InvokeAsync(() =>
        {
            if (_showOverlay || _isMenuOpen) return;
            AdjustVolume(delta);
            StateHasChanged();
        });
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
            StateHasChanged();
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
                _showOverlay = false;
                StateHasChanged();
                if (!SyncPlaySidebarOpen)
                    await _overlayRef.FocusAsync();
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
        });
    }

    private void OnSeekDebounceElapsed(object? sender, ElapsedEventArgs args)
    {
        if (_disposed) return;

        InvokeAsync(() =>
        {
            CommitSeek();
            StateHasChanged();
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
        System.Diagnostics.Debug.WriteLine("[K7-Player] overlay close click");
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

        if (_showOverlay)
        {
            HideOverlay();
            return;
        }

        OnCloseButtonClick();
    }

    private void HandleBack()
    {
        if (_disposed) return;

        System.Diagnostics.Debug.WriteLine("[K7-Player] overlay HandleBack (layer/spatial nav)");
        InvokeAsync(async () =>
        {
            PerformBackStep();
            await ReattachLayerCallbackAsync();
            StateHasChanged();
        });
    }

    private async Task ReattachLayerCallbackAsync()
    {
        if (_overlayCloseRef is null)
            return;

        try
        {
            var activeLayer = SyncPlaySidebarOpen ? ContainerRef : _overlayRef;
            await SpatialNav.AttachLayerCallbackAsync(activeLayer, _overlayCloseRef);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
    }

    private void OnBackPressed()
    {
        System.Diagnostics.Debug.WriteLine("[K7-Player] overlay OnBackPressed (PlayerService.BackPressed)");
        HandleBack();
    }

    private void OnOverlayMouseMove(MouseEventArgs args)
    {
        if (ShouldIgnoreMouseOverlayShow() || _isMouseOverControlsBar || _isMenuOpen)
        {
            return;
        }

        _showOverlay = true;
        ResetOverlayTimeout();
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
        System.Diagnostics.Debug.WriteLine("[K7-Player] overlay tap");
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
        _doubleTapSeekCount++;
        var seekStep = isRightHalf ? 10.0 : -10.0;
        _doubleTapSide = isRightHalf ? "right" : "left";
        _hudIcon = isRightHalf ? Phosphor.FastForward : Phosphor.Rewind;
        ShowHud($"{(seekStep > 0 ? "+" : "")}{(int)(seekStep * _doubleTapSeekCount)}s");

        var target = Math.Clamp(PlayerService.CurrentTime + seekStep, 0, PlayerService.Duration);
        PlayerService.Seek(target);

        _doubleTapTimer?.Stop();
        _doubleTapTimer?.Start();
        StateHasChanged();
    }

    private void OnDoubleTapTimerElapsed(object? sender, ElapsedEventArgs args)
    {
        if (_disposed) return;

        InvokeAsync(() =>
        {
            _doubleTapSide = null;
            _doubleTapSeekCount = 0;
            StateHasChanged();
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

    private void OnSeekBarDragChanged(bool draging)
    {
        if (draging)
        {
            ResetOverlayTimeout();
        }
    }

    private void OnSourceChanged(PlayerSource source) =>
        _ = EnsureMediaSegmentsLoadedAsync(source.MediaId);

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
        _showOverlay = true;
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
        System.Diagnostics.Debug.WriteLine("[K7-Player] overlay play/pause click");
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
        catch (Exception ex) when (ex is JSException or InvalidOperationException) { }

        try
        {
            await SpatialNav.PopLayerAsync(_overlayRef);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException) { }

        try
        {
            await SpatialNav.PopLayerAsync(ContainerRef);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
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
        if (DeviceService.GetClientType() == ClientType.Web)
        {
            await JSRuntime.InvokeVoidAsync("hideBodyScroll", false);
        }
    }
}
