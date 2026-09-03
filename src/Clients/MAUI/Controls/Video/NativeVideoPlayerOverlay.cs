using System.Timers;
using K7.Clients.MAUI.Playback;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using K7.Shared.Interfaces;
using Microsoft.Maui.Controls.Shapes;
using DeviceType = K7.Server.Domain.Enums.DeviceType;
using MediaSegmentType = K7.Shared.Enums.MediaSegmentType;
using Timer = System.Timers.Timer;

namespace K7.Clients.MAUI.Controls.Video;

/// <summary>
/// Full native video chrome for MAUI play sessions (Android/iOS/Windows). Mirrors
/// <c>VideoPlayerControlsOverlay.razor(.cs)</c> 1:1: transport, seek bar with
/// chapter ticks/sprite preview, playback settings, cast/remote device picker, SyncPlay, skip
/// segment, next episode, and touch/pan gestures. Binds to <see cref="IPlayerService"/>.
/// </summary>
public sealed partial class NativeVideoPlayerOverlay : Grid
{
    private readonly IPlayerService _player;
    private readonly IDeviceService _deviceService;
    private readonly IMediaService? _mediaService;
    private readonly IUserPreferencesService? _prefs;
    private readonly ISyncPlayService? _syncPlay;
    private readonly ICastOrchestrationService? _castOrchestration;
    private readonly ICastService? _castService;
    private readonly IBrightnessService? _brightness;
    private readonly IVolumeService? _volumeService;
    private readonly PlaybackProgressTracker? _progressTracker;
    private readonly IK7ServerService? _server;
    private readonly IFeatureAccessService? _featureAccess;
    private readonly IDeviceStorageService? _deviceStorage;
    private readonly K7HubClient? _hubClient;
    private readonly IRemoteControlService? _remoteControl;

    private readonly Grid _chrome = new();
    private readonly BoxView _chromeGradient = new() { InputTransparent = true };
    private readonly BoxView _loadingVeil = new()
    {
        Color = Colors.Black,
        InputTransparent = true,
        IsVisible = false
    };
    private readonly ActivityIndicator _loadingSpinner = new()
    {
        Color = Colors.White,
        InputTransparent = true,
        IsVisible = false,
        IsRunning = false,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center,
        WidthRequest = 48,
        HeightRequest = 48,
        // Above veil, below chrome - visible in the open middle without blocking back.
        ZIndex = 1
    };
    private readonly Border _startFailureBanner = new();
    private readonly Label _startFailureLabel = new();
    private readonly Grid _topBar = new();
    private readonly Grid _bottomBar = new();
    private readonly Label _titleLabel = new();
    private readonly Label _timeLabel = new();
    private int _tvChromeFocusIndex;
    private bool _tvFocusOnSeekBar;
    private static readonly Color TvFocusColor = NativeOverlayHover.Highlight;
    private Button? _hoveredChromeButton;
    private bool _skipSegmentHovered;
    private Button? _backButton;
    private readonly Border _seekBarFocusRing = new()
    {
        Stroke = Colors.Transparent,
        StrokeThickness = 2,
        Padding = new Thickness(4, 2),
        BackgroundColor = Colors.Transparent
    };

    private enum TvFocusSlot
    {
        Back,
        Play,
        Volume,
        SeekBar,
        Settings,
        Cast,
        SyncPlay,
        Fullscreen,
        SkipSegment
    }
    private readonly Border _hudBanner = new();
    private readonly Label _hudIconLabel = new();
    private readonly Label _hudTextLabel = new();
    private readonly Border _skipNotificationBanner = new();
    private readonly Label _skipNotificationIconLabel = new();
    private readonly Label _skipNotificationTextLabel = new();
    private readonly Button _playPauseButton = new();
    private readonly Button _volumeButton = new();
    private readonly Button _settingsButton = new();
    private readonly Button _castButton = new();
    private readonly Button _syncPlayButton = new();
    private readonly Button _fullscreenButton = new();
    private readonly Button _skipSegmentButton = new();
    private readonly Border _skipSegmentFocusRing = new()
    {
        Stroke = Colors.Transparent,
        StrokeThickness = 3,
        Padding = new Thickness(3),
        BackgroundColor = Colors.Transparent,
        HorizontalOptions = LayoutOptions.End,
        VerticalOptions = LayoutOptions.End,
        Margin = new Thickness(0, 0, 20, 116),
        IsVisible = false,
        ZIndex = 15
    };
    private readonly NativeVolumeSlider _volumeSlider = new();
    private readonly Border _volumePopover = new();
    private System.Timers.Timer? _cursorIdleTimer;
    private readonly NativeSeekBar _seekBar;
    private readonly NativePlaybackSettingsPanel _settings;
    // Split left/right catchers so vertical pan side does not depend on PointerPressed
    // (finger pans on Android often never fire PointerPressed before PanGestureRecognizer).
    private readonly Grid _gestureLayer = new();
    private readonly BoxView _leftCatcher = new();
    private readonly BoxView _rightCatcher = new();

    private bool _castPanelOpen;
    private bool _syncPlayPanelOpen;

    private DeviceType _deviceType = DeviceType.Desktop;
    private bool _showChrome = true;
    private bool _volumeOpen;
    private bool _seekScrubbing;
    private DateTime _suppressShowUntil = DateTime.MinValue;
    private DateTime _suppressCloseUntil = DateTime.MinValue;
    private Timer? _hideTimer;
    private Timer? _volumeHoverHideTimer;
    private Timer? _hudTimer;
    private Timer? _skipNotificationTimer;
    private Timer? _seekDebounceTimer;
    private double _seekOffset;
    private double _seekBase;
    private bool _accumulateSeeking;
    private int _scrubRepeatCount;
    private DateTime _lastScrubUtc = DateTime.MinValue;
    private Timer? _dpadHoldTimer;
    private string? _dpadHoldKey;
    private bool _dpadHoldScrubArmed;
    private double? _skipAnchorSeconds;
    private DateTime _skipAnchorUtc;
    /// <summary>Dialog-style layer (next-episode) that owns all input and hides chrome.</summary>
    private bool _inputModalActive;
    private bool _startFailureVisible;
    private static readonly TimeSpan DpadHoldScrubDelay = TimeSpan.FromMilliseconds(320);
    private static readonly TimeSpan DpadHoldInterval = TimeSpan.FromMilliseconds(110);
    private IReadOnlyList<MediaSegmentDto>? _segments;
    private SkipSegmentPresenter.State _skipState;
    private VideoPlayerSettingsDto? _videoSettings;
    private bool _showChapterTicks = true;
    private Guid? _segmentsMediaId;
    private bool _handlingPlaybackEnded;

    private static readonly TimeSpan OverlayTimeoutDesktop = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan OverlayTimeoutTv = TimeSpan.FromSeconds(5);

    public NativeVideoPlayerOverlay(
        IPlayerService player,
        IDeviceService deviceService,
        IMediaService? mediaService = null,
        IUserPreferencesService? prefs = null,
        ISyncPlayService? syncPlay = null,
        ICastOrchestrationService? castOrchestration = null,
        ICastService? castService = null,
        IBrightnessService? brightness = null,
        IVolumeService? volumeService = null,
        PlaybackProgressTracker? progressTracker = null,
        IK7ServerService? server = null,
        IFeatureAccessService? featureAccess = null,
        IDeviceStorageService? deviceStorage = null,
        K7HubClient? hubClient = null,
        IRemoteControlService? remoteControl = null)
    {
        _player = player;
        _deviceService = deviceService;
        _mediaService = mediaService;
        _prefs = prefs;
        _syncPlay = syncPlay;
        _castOrchestration = castOrchestration;
        _castService = castService;
        _brightness = brightness;
        _volumeService = volumeService;
        _progressTracker = progressTracker;
        _server = server;
        _featureAccess = featureAccess;
        _deviceStorage = deviceStorage;
        _hubClient = hubClient;
        _remoteControl = remoteControl;
        _seekBar = new NativeSeekBar { Player = player, HorizontalOptions = LayoutOptions.Fill };
        _settings = new NativePlaybackSettingsPanel(player)
        {
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(16, 16, 16, 88)
        };

        BackgroundColor = Colors.Transparent;
        IsVisible = false;
        InputTransparent = false;
        // Match MediaElement edge-to-edge so the scrim bleeds under system bars/cutouts
        // (RootGrid is SafeAreaEdges=None; layouts default to Container and would inset).
        SafeAreaEdges = SafeAreaEdges.None;

        BuildLayout();
        WireEvents();
        InitializePlaybackStats();
#if ANDROID
        HandlerChanged += (_, _) => SyncTvSurfaceComposition();
        SizeChanged += (_, _) => SyncTvSurfaceComposition();
#endif
        SizeChanged += (_, _) => UpdateSettingsAvailableHeight();
        if (NativePointerInput.SupportsHoverRecognizers)
        {
            var pointerMove = new PointerGestureRecognizer();
            pointerMove.PointerMoved += OnDesktopPointerMoved;
            GestureRecognizers.Add(pointerMove);
        }
    }

    public bool IsChromeVisible =>
        !_inputModalActive
        && (_showChrome || _settings.IsOpen || _volumeOpen || _seekScrubbing || _castPanelOpen || _syncPlayPanelOpen);

    private bool IsSkipSegmentOffered =>
        _skipSegmentFocusRing.IsVisible && _skipState.ActiveSegment is not null;

    public void Attach()
    {
        _deviceType = _deviceService.CachedDeviceType ?? DeviceType.Desktop;
        ApplyDeviceChrome();
        SubscribePlayer();
        SubscribeSyncPlay();
        ResetHideTimer();
        _ = LoadPreferencesAsync();
        _ = LoadPlaybackStatsAsync();
    }

    public void Detach()
    {
        StopDpadHold();
        DisposeSeekPreview();
        UnsubscribePlayer();
        UnsubscribeSyncPlay();
        StopHideTimer();
        CancelVolumeHoverHide();
        StopSkipNotificationTimer();
        _settings.Close();
        _volumeOpen = false;
        _volumePopover.IsVisible = false;
        SetCastPanelOpen(false);
        SetSyncPlayPanelOpen(false);
        DismissNextEpisode();
        RestoreBrightness();
        StopCursorIdle();
        DetachStatsHud();
#if WINDOWS
        Platforms.Windows.WindowsIdleCursor.Show();
#endif
    }

    private void RestoreBrightness()
    {
        _brightness?.ResetBrightness();
        _brightnessDimOverlay.Opacity = 0;
    }

    public void SetActive(bool active)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            NativeVideoDebug.Log("SetActive active=" + active + " device=" + _deviceType);
            if (active)
            {
                IsVisible = true;
                ClearStartFailure();
                AttachSidecarLayer();
                Attach();
                _awaitingFirstFrame = true;
#if ANDROID
                _tvResyncPending = true;
#endif
                SetLoadingVeil(true);
                // Warm settings UI while the veil is up so the first Open does not hitch playback.
                try { _settings.Rebuild(); } catch { /* ignore */ }
                // Keep chrome visible until the first frame so Back/Settings stay reachable
                // if Direct Play dies at t=0 (TV previously started with chrome hidden).
                ShowChrome();
                ResetSkipSession();
                _ = RefreshSegmentsAsync();
                RefreshSeekChapters();
                UpdateTransport();
                WarmSeekThumbnails();
                RefreshSidecarSubtitles();
                ApplyPlaybackStatsHud();
            }
            else
            {
                if (_player.IsFullScreen)
                {
                    _player.IsFullScreen = false;
                    _player.ExitFullScreen();
                }

                HideChrome(force: true);
                ClearStartFailure();
                _awaitingFirstFrame = false;
#if ANDROID
                Platforms.Android.AndroidOverlayComposition.Reset(this);
#endif
                ClearSidecarSubtitles();
                DetachSidecarLayer();
                SetLoadingVeil(false);
                Detach();
                IsVisible = false;
            }
        });
    }

    private bool _awaitingFirstFrame = true;
#if ANDROID
    private bool _tvResyncPending;
#endif

    /// <summary>Black cover until the first decoded frame (avoids TextureView white flash).</summary>
    public void SetLoadingVeil(bool loading)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Ignore mid-play buffer/seek requests once a frame has been shown.
            if (loading && !_awaitingFirstFrame)
                return;

            if (!IsVisible)
            {
                _loadingVeil.IsVisible = false;
                _loadingSpinner.IsVisible = false;
                _loadingSpinner.IsRunning = false;
                return;
            }

            _loadingVeil.IsVisible = loading && !_startFailureVisible;
            _loadingSpinner.IsVisible = loading && !_startFailureVisible;
            _loadingSpinner.IsRunning = loading && !_startFailureVisible;
            NativeVideoDebug.Log("SetLoadingVeil loading=" + loading);
            // Keep transport/back visible while the surface is covered.
            if (loading)
            {
                StopHideTimer();
                ShowChrome();
            }
            else if (_deviceType == DeviceType.TV && !_showChrome && !_startFailureVisible)
            {
                ResetHideTimer();
            }

            SyncTvSurfaceComposition();
        });
    }

    /// <summary>
    /// Cover the decode surface again (VLC audio reopen / mid-GOP seek) until
    /// <see cref="NotifyFirstFrameReady"/> runs.
    /// </summary>
    public void ShowTransientVeil()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _awaitingFirstFrame = true;
            _loadingVeil.IsVisible = true;
            _loadingSpinner.IsVisible = !_startFailureVisible;
            _loadingSpinner.IsRunning = !_startFailureVisible;
            StopHideTimer();
            ShowChrome();
            NativeVideoDebug.Log("SetLoadingVeil loading=True transient");
            SyncTvSurfaceComposition();
        });
    }

    /// <summary>First Playing frame - drop the startup veil and allow seek without black cover.</summary>
    public void NotifyFirstFrameReady()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _awaitingFirstFrame = false;
            ClearStartFailure();
            _loadingVeil.IsVisible = false;
            _loadingSpinner.IsVisible = false;
            _loadingSpinner.IsRunning = false;
            NativeVideoDebug.Log("SetLoadingVeil loading=False firstFrame");
            ResetHideTimer();
            SyncTvSurfaceComposition();
        });
    }

    /// <summary>
    /// Direct Play (or remux) died before a frame. Keep chrome and settings reachable.
    /// </summary>
    public void ShowStartFailure(string message)
    {
        void Apply()
        {
            _startFailureVisible = true;
            _awaitingFirstFrame = false;
            _startFailureLabel.Text = message;
            _startFailureBanner.IsVisible = true;
            _loadingVeil.IsVisible = true;
            _loadingSpinner.IsVisible = false;
            _loadingSpinner.IsRunning = false;
            StopHideTimer();
            ShowChrome();
            if (_deviceType == DeviceType.TV)
                SetTvChromeFocusSlot(TvFocusSlot.Settings);
            SyncTvSurfaceComposition();
            NativeVideoDebug.Log("ShowStartFailure");
        }

        if (MainThread.IsMainThread)
            Apply();
        else
            MainThread.BeginInvokeOnMainThread(Apply);
    }

    public void ClearStartFailure()
    {
        void Apply()
        {
            if (!_startFailureVisible && !_startFailureBanner.IsVisible)
                return;

            _startFailureVisible = false;
            _startFailureBanner.IsVisible = false;
            SyncTvSurfaceComposition();
        }

        if (MainThread.IsMainThread)
            Apply();
        else
            MainThread.BeginInvokeOnMainThread(Apply);
    }

    /// <summary>TV / keyboard Back. Returns true when consumed.</summary>
    public bool HandleBack()
    {
        if (_castPanelOpen)
        {
            SetCastPanelOpen(false);
            ResetHideTimer();
            return true;
        }

        if (_syncPlayPanelOpen)
        {
            SetSyncPlayPanelOpen(false);
            ResetHideTimer();
            return true;
        }

        if (IsNextEpisodeVisible)
        {
            _ = DismissNextEpisodeAsync();
            return true;
        }

        var (action, cancelSeek, hideChrome, closeVolume) = NativeVideoBackStack.Evaluate(
            new NativeVideoBackContext
            {
                SettingsHandledBack = _settings.TryHandleBack(),
                VolumeOpen = _volumeOpen,
                SeekScrubbing = _seekScrubbing,
                SeekBarDragging = _seekBar.IsDragging,
                ShowChrome = _showChrome,
                UtcNow = DateTime.UtcNow,
                SuppressCloseUntil = _suppressCloseUntil,
                PlaybackState = _player.PlaybackState
            });

        if (action == NativeVideoBackAction.NotHandled)
            return false;

        if (cancelSeek)
        {
            CancelTvScrub();
            _suppressCloseUntil = DateTime.UtcNow.AddMilliseconds(450);
            return true;
        }

        if (closeVolume)
        {
            SetVolumeOpen(false);
            return true;
        }

        if (hideChrome)
        {
            HideChrome();
            _suppressCloseUntil = DateTime.UtcNow.AddMilliseconds(450);
            return true;
        }

        if (action == NativeVideoBackAction.Consumed)
        {
            ResetHideTimer();
            return true;
        }

        if (action == NativeVideoBackAction.HidePlayerAsync)
        {
            NativeVideoDebug.Log("HandleBack HidePlayerAsync state=" + _player.PlaybackState);
            _ = _player.HideAsync();
            return true;
        }

        NativeVideoDebug.Log("HandleBack ClosePlayer state=" + _player.PlaybackState);
        ClosePlayer();
        return true;
    }

    public bool HandleKey(string key, bool isKeyUp = false)
    {
        key = key.ToLowerInvariant();
        if (!isKeyUp)
            NativeVideoDebug.Log(
                "HandleKey key=" + key
                + " chrome=" + _showChrome
                + " chromeVis=" + _chrome.IsVisible
                + " modal=" + _inputModalActive
                + " nep=" + IsNextEpisodeVisible
                + " scrub=" + _seekScrubbing
                + " device=" + _deviceType);

        // Modal dialog (next-episode): only Back + modal keys. Chrome is fully blocked.
        if (_inputModalActive || IsNextEpisodeVisible)
        {
            if (key is "escape" or "browserback" or "goback" or "back")
            {
                if (isKeyUp)
                    return true;

                StopDpadHold();
                return HandleBack();
            }

            return HandleNextEpisodeKey(key, isKeyUp);
        }

        // Back always goes through HandleBack. KeyUp must not run it again
        // (Windows PreviewKeyUp would hide chrome then immediately close the player).
        if (key is "escape" or "browserback" or "goback" or "back")
        {
            if (isKeyUp)
                return true;

            StopDpadHold();
            NativeVideoDebug.Log("HandleKey back chrome=" + _showChrome + " scrub=" + _seekScrubbing + " settings=" + _settings.IsOpen);
            return HandleBack();
        }

        if (key is "mediastop")
        {
            ClosePlayer();
            return true;
        }

        if (key is "space" or "mediaplaypause" or "mediaplay" or "mediapause")
        {
            if (!isKeyUp)
                TogglePlayPause();
            return true;
        }

        if (VideoRemoteTransportKeys.IsOverlaySkip(key))
            return HandleMediaSkipKey(key, VideoRemoteTransportKeys.IsOverlaySkipBack(key), isKeyUp);

        if (key is "m" && IsDesktopLike())
        {
            if (!isKeyUp)
                ToggleMute();
            return true;
        }

        if (key is "f" && IsDesktopLike())
        {
            if (!isKeyUp)
                ToggleFullscreen();
            return true;
        }

        if (_settings.IsOpen || _castPanelOpen || _syncPlayPanelOpen)
        {
            if (_settings.IsOpen)
            {
                if (!isKeyUp && key is "arrowup" or "up" or "dpad_up")
                    return _settings.MoveFocus(-1);

                if (!isKeyUp && key is "arrowdown" or "down" or "dpad_down")
                    return _settings.MoveFocus(1);

                if (!isKeyUp && key is "enter" or "select" or "dpadcenter" or "dpad_center")
                    return _settings.ActivateFocused();
            }

            if (_castPanelOpen)
            {
                if (!isKeyUp && key is "arrowup" or "up" or "dpad_up")
                    return MoveCastFocus(-1);

                if (!isKeyUp && key is "arrowdown" or "down" or "dpad_down")
                    return MoveCastFocus(1);

                if (!isKeyUp && key is "enter" or "select" or "dpadcenter" or "dpad_center")
                    return ActivateCastFocus();
            }

            // Swallow remaining keys so focus never leaks to a hidden WebView.
            return true;
        }

        var left = key is "arrowleft" or "left" or "dpad_left";
        var right = key is "arrowright" or "right" or "dpad_right";
        var up = key is "arrowup" or "up" or "dpad_up";
        var down = key is "arrowdown" or "down" or "dpad_down";
        var select = key is "enter" or "select" or "dpadcenter" or "dpad_center";

        // Chrome hidden: short L/R = skip preference seconds + HUD; hold = scrub.
        if (!_showChrome)
        {
            if (left || right)
            {
                if (IsPhoneOrTablet())
                {
                    if (isKeyUp)
                        CommitAccumulateSeek();
                    else
                        AccumulateSeek(left ? -_player.SkipBackSeconds : _player.SkipForwardSeconds);
                }
                else if (isKeyUp)
                {
                    var keyName = left ? "dpad_left" : "dpad_right";
                    var wasArmed = _dpadHoldScrubArmed;
                    var pending = _dpadHoldKey;
                    StopDpadHold();
                    if (!wasArmed && pending == keyName)
                        SkipByPreference(backward: left);
                }
                else
                {
                    StartChromeHiddenDpadHold(left ? "dpad_left" : "dpad_right", left);
                }

                return true;
            }

            // System volume on TV/phone; in-app volume only on desktop.
            if (!isKeyUp && (up || down))
            {
                if (IsDesktopLike())
                {
                    AdjustVolume(up ? 0.1 : -0.1);
                    return true;
                }

                // TV: reveal chrome and land on skip when it is offered (D-pad Up/Down).
                if (IsSkipSegmentOffered)
                    ShowChromeWithTvFocus();
                return true;
            }

            if (!isKeyUp && select)
            {
                if (IsSkipSegmentOffered)
                    SkipActiveSegment();
                else
                    ShowChromeWithTvFocus();
                return true;
            }

            return true;
        }

        // Chrome visible: L/R navigate focus (Blazor SpatialNav). Scrub only in seek-edit mode.
        if (_seekScrubbing && !isKeyUp && select)
        {
            StopDpadHold();
            CommitTvScrub();
            return true;
        }

        if (left || right)
        {
            if (isKeyUp)
            {
                StopDpadHold();
                return true;
            }

            if (_seekScrubbing)
            {
                TvScrub(left ? -1 : 1);
                StartDpadHold(left ? "dpad_left" : "dpad_right");
            }
            else
            {
                MoveTvChromeFocus(left ? -1 : 1);
            }

            return true;
        }

        if (!isKeyUp && (up || down))
        {
            StopDpadHold();
            if (_seekScrubbing)
                CancelTvScrub();

            if (TryMoveTvFocusToSkipSegment(up))
            {
                ResetHideTimer();
                return true;
            }

            MoveTvChromeFocus(up ? -1 : 1);
            return true;
        }

        if (!isKeyUp && select)
        {
            StopDpadHold();
            if (_tvFocusOnSeekBar)
            {
                if (_seekScrubbing)
                    CommitTvScrub();
                else
                    BeginTvSeekEdit();
            }
            else
            {
                ActivateTvChromeFocus();
            }

            return true;
        }

        ResetHideTimer();
        return true;
    }

    private void BeginTvSeekEdit()
    {
        _seekBar.BeginEdit();
        _seekScrubbing = true;
        _tvFocusOnSeekBar = true;
        StopHideTimer();
        UpdateSeekPreview(true);
        ApplyTvChromeFocusHighlight();
        NativeVideoDebug.Log("SeekBar edit begin");
    }

    private void CommitTvScrub()
    {
        StopDpadHold();
        _seekBar.CommitEdit();
        _seekScrubbing = false;
        UpdateSeekPreview(false);
        ResetHideTimer();
        NativeVideoDebug.Log("SeekBar commit time=" + _player.CurrentTime.ToString("F1") + "s");
    }

    private void CancelTvScrub()
    {
        StopDpadHold();
        _seekBar.CancelEdit();
        _seekScrubbing = false;
        UpdateSeekPreview(false);
        NativeVideoDebug.Log("SeekBar cancel");
    }

    private void StartDpadHold(string key)
    {
        StopDpadHoldTimerOnly();
        _dpadHoldKey = key;
        _dpadHoldScrubArmed = true;
        _dpadHoldTimer = new Timer(DpadHoldInterval.TotalMilliseconds) { AutoReset = true };
        _dpadHoldTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(OnDpadHoldTick);
        _dpadHoldTimer.Start();
    }

    private void StartChromeHiddenDpadHold(string key, bool left)
    {
        StopDpadHold();
        _dpadHoldKey = key;
        _dpadHoldScrubArmed = false;
        _dpadHoldTimer = new Timer(DpadHoldScrubDelay.TotalMilliseconds) { AutoReset = false };
        _dpadHoldTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_dpadHoldKey != key)
                return;

            _dpadHoldScrubArmed = true;
            TvScrub(left ? -1 : 1);
            StartDpadHold(key);
        });
        _dpadHoldTimer.Start();
    }

    private void StopDpadHold()
    {
        StopDpadHoldTimerOnly();
        _dpadHoldKey = null;
        _dpadHoldScrubArmed = false;
    }

    private void StopDpadHoldTimerOnly()
    {
        _dpadHoldTimer?.Stop();
        _dpadHoldTimer?.Dispose();
        _dpadHoldTimer = null;
    }

    private void OnDpadHoldTick()
    {
        if (_dpadHoldKey is null || !_dpadHoldScrubArmed || IsNextEpisodeVisible)
            return;

        if (_dpadHoldKey is "dpad_left" or "dpad_right"
            || VideoRemoteTransportKeys.IsOverlaySkip(_dpadHoldKey))
        {
            var backward = _dpadHoldKey is "dpad_left"
                || VideoRemoteTransportKeys.IsOverlaySkipBack(_dpadHoldKey);
            if (VideoRemoteTransportKeys.IsOverlaySkip(_dpadHoldKey) || !_showChrome || _seekScrubbing)
                TvScrub(backward ? -1 : 1);
            else
                StopDpadHold();
        }
    }

    /// <summary>
    /// Dedicated FF/RW: always skip by preference (even with chrome visible). Hold scrubs.
    /// </summary>
    private bool HandleMediaSkipKey(string key, bool backward, bool isKeyUp)
    {
        if (IsPhoneOrTablet())
        {
            if (isKeyUp)
                CommitAccumulateSeek();
            else
                AccumulateSeek(backward ? -_player.SkipBackSeconds : _player.SkipForwardSeconds);
            return true;
        }

        if (isKeyUp)
        {
            var wasArmed = _dpadHoldScrubArmed;
            var pending = _dpadHoldKey;
            StopDpadHold();
            if (!wasArmed && pending == key)
                SkipByPreference(backward);
            return true;
        }

        StartChromeHiddenDpadHold(key, left: backward);
        return true;
    }

    private void SkipByPreference(bool backward)
    {
        var delta = backward ? -_player.SkipBackSeconds : _player.SkipForwardSeconds;
        if (Math.Abs(delta) < 0.1)
            delta = backward ? -10 : 10;

        var duration = Math.Max(_player.Duration, 0);
        var anchor = GetSkipAnchorSeconds();
        var target = Math.Clamp(anchor + delta, 0, duration > 0 ? duration : double.MaxValue);
        RememberSkipAnchor(target);
        _player.Seek(target);
        var seconds = (int)Math.Round(Math.Abs((double)delta));
        var label = (delta >= 0 ? "+" : "-") + seconds + " s";
        ShowHud(label, delta >= 0 ? NativePlayerGlyphs.Forward : NativePlayerGlyphs.Rewind);
        UpdateTimeLabel();
        NativeVideoDebug.Log("SkipByPreference delta=" + delta.ToString("F0") + "s target=" + target.ToString("F1") + "s");
    }

    private double GetSkipAnchorSeconds()
    {
        if (_skipAnchorSeconds is double chained
            && (DateTime.UtcNow - _skipAnchorUtc).TotalMilliseconds < 900)
            return chained;

        return Math.Max(0, _player.CurrentTime);
    }

    private void RememberSkipAnchor(double seconds)
    {
        _skipAnchorSeconds = seconds;
        _skipAnchorUtc = DateTime.UtcNow;
    }

    private void BuildLayout()
    {
        _gestureLayer.ColumnDefinitions =
        [
            new ColumnDefinition(GridLength.Star),
            new ColumnDefinition(GridLength.Star)
        ];
        ConfigureGestureCatcher(_leftCatcher, panSideLeft: true);
        ConfigureGestureCatcher(_rightCatcher, panSideLeft: false);
        Grid.SetColumn(_leftCatcher, 0);
        Grid.SetColumn(_rightCatcher, 1);
        _gestureLayer.Add(_leftCatcher);
        _gestureLayer.Add(_rightCatcher);
        Children.Add(_gestureLayer);

        BuildGestureVisuals();

        // Full-bleed fade matching Blazor .video-controls-overlay::before (transparent -> black).
        _chromeGradient.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, 1),
            GradientStops =
            [
                new GradientStop(Color.FromArgb("#00000000"), 0f),
                new GradientStop(Color.FromArgb("#00000000"), 0.35f),
                new GradientStop(Color.FromArgb("#99000000"), 0.65f),
                new GradientStop(Color.FromArgb("#E6000000"), 1f)
            ]
        };
        _chromeGradient.Opacity = 0.85;
        _chromeGradient.InputTransparent = true;
        Children.Add(_chromeGradient);

        BuildSidecarSubtitleLabel();

        // Veil + spinner under chrome: cover the video surface, keep back/controls usable.
        _loadingVeil.Color = Colors.Black;
        _loadingVeil.InputTransparent = true;
        _loadingVeil.ZIndex = 0;
        Children.Add(_loadingVeil);
        DisableHitTesting(_loadingVeil);
        _loadingSpinner.ZIndex = 1;
        Children.Add(_loadingSpinner);
        DisableHitTesting(_loadingSpinner);

        _chrome.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _chrome.RowDefinitions.Add(new RowDefinition { Height = GridLength.Star });
        _chrome.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        // Empty middle passes taps to gesture catchers; bars must still receive touches.
        // CascadeInputTransparent=false is required: with the default (true), children of
        // an InputTransparent parent never get input on Android (tap -> hide chrome).
        _chrome.InputTransparent = true;
        _chrome.CascadeInputTransparent = false;
        // Keep transport clear of notches/system bars while the scrim sibling stays full-bleed.
        _chrome.SafeAreaEdges = new SafeAreaEdges(SafeAreaRegions.Container);
        _chrome.ZIndex = 10;

        BuildTopBar();
        BuildBottomBar();
        Grid.SetRow(_topBar, 0);
        Grid.SetRow(_bottomBar, 2);
        _chrome.Children.Add(_topBar);
        _chrome.Children.Add(_bottomBar);
        _topBar.InputTransparent = false;
        _bottomBar.InputTransparent = false;
        Children.Add(_chrome);

        _hudIconLabel.TextColor = Colors.White;
        _hudIconLabel.FontSize = 28;
        _hudIconLabel.FontFamily = NativePlayerGlyphs.FontFamily;
        _hudIconLabel.VerticalOptions = LayoutOptions.Center;
        _hudTextLabel.TextColor = Colors.White;
        _hudTextLabel.FontSize = 28;
        _hudTextLabel.FontAttributes = FontAttributes.Bold;
        _hudTextLabel.VerticalOptions = LayoutOptions.Center;
        _hudBanner.Content = new HorizontalStackLayout
        {
            Spacing = 10,
            Children = { _hudIconLabel, _hudTextLabel }
        };
        _hudBanner.Padding = new Thickness(20, 12);
        _hudBanner.BackgroundColor = Color.FromArgb("#99000000");
        _hudBanner.Stroke = Colors.Transparent;
        _hudBanner.HorizontalOptions = LayoutOptions.Center;
        _hudBanner.VerticalOptions = LayoutOptions.Center;
        _hudBanner.IsVisible = false;
        Children.Add(_hudBanner);

        _skipNotificationIconLabel.TextColor = Colors.White;
        _skipNotificationIconLabel.FontSize = 14;
        _skipNotificationIconLabel.FontFamily = NativePlayerGlyphs.FontFamily;
        _skipNotificationIconLabel.VerticalOptions = LayoutOptions.Center;
        _skipNotificationTextLabel.TextColor = Colors.White;
        _skipNotificationTextLabel.FontSize = 14;
        _skipNotificationTextLabel.VerticalOptions = LayoutOptions.Center;
        _skipNotificationBanner.Content = new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { _skipNotificationIconLabel, _skipNotificationTextLabel }
        };
        _skipNotificationBanner.Padding = new Thickness(14, 8);
        _skipNotificationBanner.BackgroundColor = Color.FromArgb("#CC000000");
        _skipNotificationBanner.Stroke = Colors.Transparent;
        _skipNotificationBanner.HorizontalOptions = LayoutOptions.Center;
        _skipNotificationBanner.VerticalOptions = LayoutOptions.Start;
        _skipNotificationBanner.Margin = new Thickness(0, 84, 0, 0);
        _skipNotificationBanner.IsVisible = false;
        Children.Add(_skipNotificationBanner);

        _startFailureLabel.TextColor = Colors.White;
        _startFailureLabel.FontSize = 16;
        _startFailureLabel.HorizontalTextAlignment = TextAlignment.Center;
        _startFailureLabel.LineBreakMode = LineBreakMode.WordWrap;
        _startFailureBanner.Content = _startFailureLabel;
        _startFailureBanner.Padding = new Thickness(20, 12);
        _startFailureBanner.BackgroundColor = Color.FromArgb("#CC000000");
        _startFailureBanner.Stroke = Colors.Transparent;
        _startFailureBanner.HorizontalOptions = LayoutOptions.Center;
        _startFailureBanner.VerticalOptions = LayoutOptions.Center;
        _startFailureBanner.Margin = new Thickness(24, 0);
        _startFailureBanner.MaximumWidthRequest = 720;
        _startFailureBanner.ZIndex = 12;
        _startFailureBanner.IsVisible = false;
        Children.Add(_startFailureBanner);

        _skipSegmentButton.Text = NativeStrings.SkipIntro;
        _skipSegmentButton.BackgroundColor = Color.FromArgb("#CCFFFFFF");
        _skipSegmentButton.TextColor = Colors.Black;
        _skipSegmentButton.Padding = new Thickness(16, 10);
        _skipSegmentButton.CornerRadius = 8;
        _skipSegmentButton.HorizontalOptions = LayoutOptions.Center;
        _skipSegmentButton.Clicked += (_, _) => SkipActiveSegment();
        DisablePlatformFocus(_skipSegmentButton);
        NativeOverlayHover.ApplyHandCursor(_skipSegmentButton);
        NativeOverlayHover.Attach(_skipSegmentFocusRing, hovered =>
        {
            _skipSegmentHovered = hovered;
            ApplyTvChromeFocusHighlight();
        });
        _skipSegmentFocusRing.StrokeShape = new RoundRectangle { CornerRadius = 10 };
        _skipSegmentFocusRing.Content = _skipSegmentButton;
        Children.Add(_skipSegmentFocusRing);

        BuildDevicePanel();
        BuildSyncPlayPanel();
        BuildSeekPreview();
        BuildNextEpisodeOverlay();
        BuildReactionLayer();

        Children.Add(_settings);
    }

    private void BuildTopBar()
    {
        // Transparent bar - darkness comes from the full-bleed chrome gradient (Blazor parity).
        _topBar.BackgroundColor = Colors.Transparent;
        _topBar.Padding = new Thickness(12, 16);
        _topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _topBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });

        var back = CreateIconButton(NativePlayerGlyphs.Back);
        back.Clicked += (_, _) => ClosePlayer();
        _backButton = back;
        _titleLabel.TextColor = Colors.White;
        _titleLabel.FontSize = 18;
        _titleLabel.LineBreakMode = LineBreakMode.TailTruncation;
        _titleLabel.VerticalOptions = LayoutOptions.Center;
        _titleLabel.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(_titleLabel, 1);
        _topBar.Children.Add(back);
        _topBar.Children.Add(_titleLabel);
    }

    private void BuildBottomBar()
    {
        _bottomBar.BackgroundColor = Colors.Transparent;
        _bottomBar.Padding = new Thickness(12, 10, 12, 16);
        _bottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _bottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star });
        _bottomBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _bottomBar.ColumnSpacing = 8;

        var left = new HorizontalStackLayout { Spacing = 8, VerticalOptions = LayoutOptions.Center };
        _playPauseButton.Text = NativePlayerGlyphs.Play;
        StyleTransportButton(_playPauseButton);
        _playPauseButton.Clicked += (_, _) => TogglePlayPause();
        left.Children.Add(_playPauseButton);

        _volumeButton.Text = NativePlayerGlyphs.SpeakerHigh;
        StyleTransportButton(_volumeButton);
        _volumeButton.Clicked += (_, _) => ToggleMute();
        AttachVolumeHover();
        left.Children.Add(_volumeButton);

        _volumeSlider.Value = DisplayedVolume;
        _volumeSlider.ValueChanged += (_, value) =>
        {
            CancelVolumeHoverHide();
            ApplyUserVolume(value);
            UpdateTransport();
            ResetHideTimer();
        };

        var plus = new Label
        {
            Text = "+",
            TextColor = Colors.White,
            FontSize = 22,
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Center,
            FontAutoScalingEnabled = false
        };
        var minus = new Label
        {
            Text = "\u2212",
            TextColor = Colors.White,
            FontSize = 22,
            HorizontalTextAlignment = TextAlignment.Center,
            HorizontalOptions = LayoutOptions.Center,
            FontAutoScalingEnabled = false
        };
        var volumeStack = new VerticalStackLayout
        {
            Spacing = 4,
            Padding = new Thickness(4, 8),
            Children = { plus, _volumeSlider, minus }
        };
        _volumePopover.Content = volumeStack;
        _volumePopover.BackgroundColor = Color.FromArgb("#EB1E1E1E");
        _volumePopover.Padding = 0;
        _volumePopover.Stroke = Color.FromArgb("#1AFFFFFF");
        _volumePopover.StrokeThickness = 1;
        _volumePopover.StrokeShape = new RoundRectangle { CornerRadius = 16 };
        _volumePopover.IsVisible = false;
        _volumePopover.HorizontalOptions = LayoutOptions.Start;
        _volumePopover.VerticalOptions = LayoutOptions.End;
        _volumePopover.Margin = new Thickness(52, 0, 0, 72);
        Children.Add(_volumePopover);
        AttachVolumePopoverHover();

        _seekBar.HoverChanged += OnSeekHover;
        _seekBar.HoverEnded += OnSeekHoverEnded;
        _seekBar.PreviewMoved += OnSeekPreviewMoved;

        if (NativePointerInput.SupportsHoverRecognizers)
        {
            var seekPointer = new PointerGestureRecognizer();
            seekPointer.PointerMoved += OnSeekRingPointerMoved;
            seekPointer.PointerExited += OnSeekRingPointerExited;
            _seekBarFocusRing.GestureRecognizers.Add(seekPointer);
        }

        _timeLabel.TextColor = Colors.White;
        _timeLabel.FontSize = 13;
        _timeLabel.VerticalOptions = LayoutOptions.Center;
        left.Children.Add(_timeLabel);

        Grid.SetColumn(left, 0);
        _seekBarFocusRing.Content = _seekBar;
        _seekBarFocusRing.StrokeShape = new RoundRectangle { CornerRadius = 4 };
        Grid.SetColumn(_seekBarFocusRing, 1);
        _seekBar.VerticalOptions = LayoutOptions.Center;
        _seekBar.DragChanged += OnSeekDragChanged;

        var right = new HorizontalStackLayout { Spacing = 4, VerticalOptions = LayoutOptions.Center };
        _settingsButton.Text = NativePlayerGlyphs.Settings;
        StyleTransportButton(_settingsButton);
        _settingsButton.Clicked += (_, _) =>
        {
            if (_settings.IsOpen)
                _settings.Close();
            else
                _settings.Open();
            ResetHideTimer(TimeSpan.FromSeconds(5));
        };
        right.Children.Add(_settingsButton);

        _castButton.Text = NativePlayerGlyphs.Cast;
        StyleTransportButton(_castButton);
        _castButton.IsVisible = HasAnyCastOrRemoteDevice();
        _castButton.Clicked += (_, _) => ToggleCastPanel();
        right.Children.Add(_castButton);

        _syncPlayButton.Text = NativePlayerGlyphs.SyncPlay;
        StyleTransportButton(_syncPlayButton);
        _syncPlayButton.IsVisible = _syncPlay?.IsInGroup == true;
        _syncPlayButton.Clicked += (_, _) => ToggleSyncPlayPanel();
        right.Children.Add(_syncPlayButton);

        _fullscreenButton.Text = NativePlayerGlyphs.FullscreenEnter;
        StyleTransportButton(_fullscreenButton);
        _fullscreenButton.Clicked += (_, _) => ToggleFullscreen();
        right.Children.Add(_fullscreenButton);

        Grid.SetColumn(right, 2);
        _bottomBar.Children.Add(left);
        _bottomBar.Children.Add(_seekBarFocusRing);
        _bottomBar.Children.Add(right);
    }

    private void StyleTransportButton(Button button)
    {
        button.BackgroundColor = Colors.Transparent;
        button.BorderColor = Colors.Transparent;
        button.BorderWidth = 0;
        button.TextColor = Colors.White;
        button.FontFamily = NativePlayerGlyphs.FontFamily;
        button.FontSize = 20;
        button.Padding = new Thickness(10, 6);
        button.CornerRadius = 8;
        button.FontAutoScalingEnabled = false;
        // TV uses software focus rings; native Button focus steals DPAD from next-episode.
        DisablePlatformFocus(button);
        NativeOverlayHover.Attach(button, hovered =>
        {
            _hoveredChromeButton = hovered ? button : (ReferenceEquals(_hoveredChromeButton, button) ? null : _hoveredChromeButton);
            ApplyTvChromeFocusHighlight();
        });
    }

    private Button CreateIconButton(string glyph)
    {
        var button = new Button { Text = glyph };
        StyleTransportButton(button);
        return button;
    }

    /// <summary>Prevent Android View focus navigation from hijacking DPAD (software focus only).</summary>
    private static void DisablePlatformFocus(VisualElement element)
    {
        void Apply()
        {
#if ANDROID
            if (element.Handler?.PlatformView is Android.Views.View view)
            {
                view.Focusable = false;
                view.FocusableInTouchMode = false;
            }
#endif
#if WINDOWS
            if (element.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Control control)
            {
                control.BorderThickness = new Microsoft.UI.Xaml.Thickness(0);
                control.UseSystemFocusVisuals = false;
            }
#endif
        }

        Apply();
        element.HandlerChanged += (_, _) => Apply();
    }

    /// <summary>
    /// InputTransparent alone is not enough for WinUI ActivityIndicator/BoxView - they can
    /// still eat pointer hits and block the back button above the loading veil.
    /// </summary>
    private static void DisableHitTesting(VisualElement element)
    {
        void Apply()
        {
#if WINDOWS
            if (element.Handler?.PlatformView is Microsoft.UI.Xaml.UIElement ui)
                ui.IsHitTestVisible = false;
#endif
#if ANDROID
            if (element.Handler?.PlatformView is Android.Views.View view)
                view.Clickable = false;
#endif
        }

        Apply();
        element.HandlerChanged += (_, _) => Apply();
    }

    private void WireEvents()
    {
        _settings.OpenedChanged += (_, _) =>
        {
            if (_settings.IsOpen)
            {
                // Only one side panel at a time (settings vs cast/remote vs SyncPlay).
                SetCastPanelOpen(false);
                SetSyncPlayPanelOpen(false);
            }

            UpdateSettingsAvailableHeight();
            UpdateChromeVisibility();
            ResetHideTimer(TimeSpan.FromSeconds(5));
            LogPlaybackMenuSnapshot(_settings.IsOpen ? "settings-open" : "settings-close");
        };
    }

    private void LogPlaybackMenuSnapshot(string reason)
    {
        NativeVideoDebug.Warn(
            reason
            + " mauiW=" + _settings.Width.ToString("0")
            + " mauiH=" + _settings.Height.ToString("0")
            + " chrome=" + IsChromeVisible);
#if ANDROID
        FindBlazorPage()?.LogVideoSurfaceSnapshot(reason, _settings);
        if (_settings.Handler?.PlatformView is Android.Views.View view)
        {
            view.Post(() => FindBlazorPage()?.LogVideoSurfaceSnapshot(reason + "-post", _settings));
            view.PostDelayed(
                () => FindBlazorPage()?.LogVideoSurfaceSnapshot(reason + "-post300", _settings),
                300);
        }
#endif
    }

    private void UpdateSettingsAvailableHeight()
    {
        // Panel sits above the transport bar (same reserve as its bottom margin).
        var bottomReserve = _settings.Margin.Bottom;
        if (_bottomBar.Height > 0)
            bottomReserve = Math.Max(bottomReserve, _bottomBar.Height);

        var available = Height - _settings.Margin.Top - bottomReserve;
        _settings.SetAvailableHeight(available);
    }

    private void SubscribePlayer()
    {
        _player.PlaybackStateChanged += OnPlayerChanged;
        _player.CurrentTimeChanged += OnTimeChanged;
        _player.BufferedTimeChanged += OnBufferedChanged;
        _player.VolumeChanged += OnVolumeChanged;
        _player.IsMutedChanged += OnMutedChanged;
        _player.IsFullScreenChanged += OnMutedChanged;
        _player.SourceChanged += OnSourceChanged;
        _player.BackPressed += OnBackPressed;
        _player.AudioTrackChanged += _ =>
        {
            if (_settings.IsOpen)
                MainThread.BeginInvokeOnMainThread(_settings.Rebuild);
        };
        _player.SubtitleTrackChanged += OnSubtitleTrackChanged;
        _player.QualityChanged += _ =>
        {
            if (_settings.IsOpen)
                MainThread.BeginInvokeOnMainThread(_settings.Rebuild);
        };
        _player.AspectRatioModeChanged += _ =>
        {
            if (_settings.IsOpen)
                MainThread.BeginInvokeOnMainThread(_settings.Rebuild);
        };
        _player.PlaybackRateChanged += _ =>
        {
            if (_settings.IsOpen)
                MainThread.BeginInvokeOnMainThread(_settings.Rebuild);
        };
        _player.PlayerUxSettingsChanged += OnPlayerUxSettingsChanged;
    }

    private void UnsubscribePlayer()
    {
        _player.PlaybackStateChanged -= OnPlayerChanged;
        _player.CurrentTimeChanged -= OnTimeChanged;
        _player.BufferedTimeChanged -= OnBufferedChanged;
        _player.VolumeChanged -= OnVolumeChanged;
        _player.IsMutedChanged -= OnMutedChanged;
        _player.IsFullScreenChanged -= OnMutedChanged;
        _player.SourceChanged -= OnSourceChanged;
        _player.BackPressed -= OnBackPressed;
        _player.SubtitleTrackChanged -= OnSubtitleTrackChanged;
        _player.PlayerUxSettingsChanged -= OnPlayerUxSettingsChanged;
    }

    private void OnPlayerUxSettingsChanged() =>
        MainThread.BeginInvokeOnMainThread(ApplyVideoPlayerUxSettingsFromPlayer);

    private void ApplyVideoPlayerUxSettingsFromPlayer()
    {
        var settings = _player.VideoPlayerUxSettings;
        if (settings is null)
            return;

        _videoSettings = settings;
        _showChapterTicks = settings.ShowChapterTicks;
#if ANDROID
        K7.Clients.MAUI.Platforms.Android.AndroidSubtitleStyle.SetSettings(_videoSettings);
        TryApplyAndroidSubtitleStyle();
#elif WINDOWS
        VlcSubtitleStyle.SetSettings(_videoSettings);
        TryApplyWindowsSubtitleStyle();
#endif
        ApplySidecarSubtitleStyle();
        RefreshSeekChapters();
        ApplySkipSegmentOnMainThread();
    }

    private void OnSubtitleTrackChanged(SubtitleFileTrackDto? _) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_settings.IsOpen)
                _settings.Rebuild();
            RefreshSidecarSubtitles();
        });

    private void OnBackPressed() => MainThread.BeginInvokeOnMainThread(() => HandleBack());

    private void OnPlayerChanged(PlaybackState state) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Windows HLS (Video.js): no LibVLC FirstFrame - lift the veil once Playing.
            if (state == PlaybackState.Playing && _awaitingFirstFrame)
                NotifyFirstFrameReady();

            UpdateTransport();
            if (state == PlaybackState.Ended)
                OnPlaybackEnded();
            else if (state == PlaybackState.Playing && IsNextEpisodeVisible)
                DismissNextEpisode();
        });

    private void OnMutedChanged(bool _) => MainThread.BeginInvokeOnMainThread(UpdateTransport);
    private void OnVolumeChanged(double _) => MainThread.BeginInvokeOnMainThread(UpdateTransport);

    private void OnTimeChanged(double time)
    {
        void Apply()
        {
            // Chrome-hidden TV: avoid GraphicsView.Invalidate + label churn every Exo tick
            // (500ms). Amlogic HDMI composition hitches when the overlay layer keeps redrawing.
            if (IsChromeVisible || _seekScrubbing || _accumulateSeeking)
            {
                UpdateTimeLabel();
                _seekBar.Refresh();
            }

            UpdateSkipSegment(time);
            // Sidecar VTT follows the held resume clock; do not paint cues over the veil.
            // Android: Exo SubtitleView owns text - no XAML sidecar.
#if !ANDROID
            if (!_awaitingFirstFrame)
                UpdateSidecarCue(time);
#endif
        }

        if (MainThread.IsMainThread)
            Apply();
        else
            MainThread.BeginInvokeOnMainThread(Apply);
    }

    private void OnBufferedChanged(double _)
    {
        if (!IsChromeVisible)
            return;

        MainThread.BeginInvokeOnMainThread(_seekBar.Refresh);
    }

    private void OnSourceChanged(PlayerSource source) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DisposeSpriteBitmap();
            _titleLabel.Text = source.Title ?? string.Empty;
            _ = RefreshSegmentsAsync();
            RefreshSeekChapters();
            UpdateTransport();
            RefreshSidecarSubtitles();
        });

    private void OnSeekDragChanged(object? sender, bool dragging)
    {
        _seekScrubbing = dragging;
        UpdateSeekPreview(dragging);
        if (dragging)
            StopHideTimer();
        else
            ResetHideTimer();
        UpdateChromeVisibility();
    }

    private void OnSeekPreviewMoved(object? sender, double time)
    {
        _ = time;
        UpdateSeekPreview(true);
    }

    private void OnSeekRingPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_seekBar.IsDragging || _seekScrubbing)
            return;

        var point = e.GetPosition(_seekBar);
        if (point is null)
            return;

        _seekBar.HoverAt(point.Value.X);
    }

    private void OnSeekRingPointerExited(object? sender, PointerEventArgs e)
    {
        if (_seekBar.IsDragging || _seekScrubbing)
            return;

        _seekBar.EndHover();
    }

    private void OnSeekHover(object? sender, double time)
    {
        if (_seekBar.IsDragging || _seekScrubbing)
            return;

        _seekBar.PreviewTime = time;
        UpdateSeekPreview(true);
    }

    private void OnSeekHoverEnded(object? sender, EventArgs e)
    {
        if (_seekBar.IsDragging || _seekScrubbing)
            return;

        _seekBar.PreviewTime = null;
        UpdateSeekPreview(false);
    }

    private void OnBackgroundTapped(object? sender, TappedEventArgs e)
    {
        if (_inputModalActive || IsNextEpisodeVisible)
            return;

        if (_settings.IsOpen)
        {
            _settings.Close();
            return;
        }

        if (_castPanelOpen || _syncPlayPanelOpen)
            return;

        if (DateTime.UtcNow < _suppressShowUntil)
            return;

        if (_showChrome)
            HideChrome();
        else
            ShowChrome();
    }

    private void ApplyDeviceChrome()
    {
        // System volume on TV / phone / tablet - hide in-app volume control.
        _volumeButton.IsVisible = IsDesktopLike();
        _fullscreenButton.IsVisible = IsDesktopLike();
        _castButton.IsVisible = HasAnyCastOrRemoteDevice();
        _syncPlayButton.IsVisible = _syncPlay?.IsInGroup == true;
        // TV uses the remote, not touch catchers. Two full-screen BoxViews over
        // SurfaceView force a GPU compose on Amlogic and drop HEVC frames.
        if (_deviceType == DeviceType.TV)
            _gestureLayer.IsVisible = false;

        SyncTvSurfaceComposition();
    }

    private void UpdateTransport()
    {
        var playing = _player.PlaybackState is PlaybackState.Playing or PlaybackState.Buffering;
        _playPauseButton.Text = playing ? NativePlayerGlyphs.Pause : NativePlayerGlyphs.Play;
        var volume = DisplayedVolume;
        _volumeButton.Text = _player.IsMuted || volume <= 0.001
            ? NativePlayerGlyphs.SpeakerMuted
            : volume < 0.4 ? NativePlayerGlyphs.SpeakerLow : NativePlayerGlyphs.SpeakerHigh;
        _volumeSlider.Value = _player.IsMuted ? 0 : volume;
        _fullscreenButton.Text = _player.IsFullScreen
            ? NativePlayerGlyphs.FullscreenExit
            : NativePlayerGlyphs.FullscreenEnter;
        _titleLabel.Text = _player.Source?.Title ?? string.Empty;
        _syncPlayButton.IsVisible = _syncPlay?.IsInGroup == true;
        _castButton.IsVisible = HasAnyCastOrRemoteDevice();
        UpdateTimeLabel();
        UpdateChromeVisibility();
    }

    private void UpdateTimeLabel()
    {
        var duration = _player.Duration;
        // Always show real playback time in the transport label. Scrub preview time lives in
        // the seek thumbnail HUD until the user commits.
        var current = _accumulateSeeking
            ? _seekBase + _seekOffset
            : _player.CurrentTime;

        if (duration <= 0)
        {
            _timeLabel.Text = "--:-- / --:--";
            return;
        }

        _timeLabel.Text =
            $"{NativeTimeFormatting.Format(current)} / {NativeTimeFormatting.Format(duration)}";
        if (_seekScrubbing || _seekBar.IsDragging)
            UpdateSeekPreview(true);
    }

    private void UpdateChromeVisibility()
    {
        var visible = IsChromeVisible;
        _chrome.IsVisible = visible;
        _chromeGradient.IsVisible = visible;
        _topBar.Opacity = visible ? 1 : 0;
        _bottomBar.Opacity = visible ? 1 : 0;
        if (!visible)
            ClearTvChromeFocus();

        SyncTvSurfaceComposition();
        NativeVideoDebug.Log(
            "Chrome visible=" + visible
            + " settings=" + _settings.IsOpen);
#if ANDROID
        if (!visible)
            FindBlazorPage()?.LogVideoSurfaceSnapshot("chrome-hide", _settings);
#endif
    }

    private void SyncTvSurfaceComposition()
    {
        if (_deviceType != DeviceType.TV)
            return;

        // Stats HUD is a RootGrid sibling, not a child of this overlay. Do not keep the
        // full-screen chrome layer drawn for it: that reintroduces the Amlogic hitch.
        var draw = IsChromeVisible
            || IsSkipSegmentOffered
            || IsNextEpisodeVisible
            || _hudBanner.IsVisible
            || _loadingVeil.IsVisible
            || _startFailureBanner.IsVisible
            || _skipNotificationBanner.IsVisible;
#if ANDROID
        Platforms.Android.AndroidOverlayComposition.SetDraws(this, draw);
        FindBlazorPage()?.EnsureVideoSurfaceNotFocusable();
#else
        TranslationX = 0;
#endif
    }

    private void ShowChrome()
    {
        if (_inputModalActive || IsNextEpisodeVisible)
            return;

        _showChrome = true;
        UpdateChromeVisibility();
        ApplySkipSegmentAtCurrentTime();
        ResetHideTimer();
        TryFocusSkipSegmentIfOffered();
    }

    private void ShowChromeWithTvFocus()
    {
        if (_inputModalActive || IsNextEpisodeVisible)
            return;

        ShowChrome();
        // Skip is the primary action while offered; otherwise play/pause
        // (Blazor SpatialNav.FocusFirstAsync(".play-pause-btn")).
        if (!IsSkipSegmentOffered)
            SetTvChromeFocusSlot(TvFocusSlot.Play);
    }

    private void HideChrome(bool force = false)
    {
        if (!force && (_settings.IsOpen || _seekScrubbing))
            return;

        // Veil covers the video surface - never hide back/controls until first frame.
        if (!force && _awaitingFirstFrame)
            return;

        if (!force && _startFailureVisible)
            return;

        if (force && _seekScrubbing)
            CancelTvScrub();

        _showChrome = false;
        SetVolumeOpen(false);
        ClearTvChromeFocus();
        _suppressShowUntil = DateTime.UtcNow.AddMilliseconds(500);
        UpdateChromeVisibility();
        StopHideTimer();
        MaybeRunTvDecodeResync();
    }

    /// <summary>
    /// On Amlogic, the first time chrome hides during playback, HEVC starts dropping ~3
    /// frames every 10s. Laying the settings panel's native view out once (the exact effect
    /// a real settings open produces) permanently clears it. This replicates that off-screen
    /// and fully transparent, so there is no visible flash and no chrome disturbance. Applied
    /// to all Android TV as a safety net (harmless where no drops occur), once per session.
    /// </summary>
    private void MaybeRunTvDecodeResync()
    {
#if ANDROID
        if (!_tvResyncPending || _awaitingFirstFrame || _settings.IsOpen)
            return;

        if (_deviceType != DeviceType.TV)
            return;

        _tvResyncPending = false;

        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(1200), () =>
        {
            if (_settings.IsOpen)
                return;

            _settings.PrewarmNativeLayout();
            NativeVideoDebug.Log("TvDecodeResync layout pulse");

            Dispatcher.DispatchDelayed(
                TimeSpan.FromMilliseconds(400),
                () => _settings.EndPrewarmNativeLayout());
        });
#endif
    }

    /// <summary>
    /// Enter/exit a dialog-style input modal. While active, transport chrome and gestures are
    /// fully blocked - only the modal layer (next-episode) receives keys and touches.
    /// </summary>
    private void SetInputModalActive(bool active)
    {
        _inputModalActive = active;
        StopDpadHold();
        StopHideTimer();

        if (active)
        {
            if (_seekScrubbing)
                CancelTvScrub();
            _settings.Close();
            SetCastPanelOpen(false);
            SetSyncPlayPanelOpen(false);
            SetVolumeOpen(false);
            _showChrome = false;
            _chrome.IsVisible = false;
            _chromeGradient.IsVisible = false;
            SetSkipSegmentVisible(false);
            _seekPreview.IsVisible = false;
            SetGestureCatchersInputTransparent(true);
            ClearTvChromeFocus();
        }
        else
        {
            SetGestureCatchersInputTransparent(false);
        }

        UpdateChromeVisibility();
        NativeVideoDebug.Log("InputModal active=" + active);
    }

    private List<TvFocusSlot> GetVisibleTvFocusSlots()
    {
        var slots = new List<TvFocusSlot> { TvFocusSlot.Back, TvFocusSlot.Play };
        if (_volumeButton.IsVisible)
            slots.Add(TvFocusSlot.Volume);
        slots.Add(TvFocusSlot.SeekBar);
        slots.Add(TvFocusSlot.Settings);
        if (_castButton.IsVisible)
            slots.Add(TvFocusSlot.Cast);
        if (_syncPlayButton.IsVisible)
            slots.Add(TvFocusSlot.SyncPlay);
        if (_fullscreenButton.IsVisible)
            slots.Add(TvFocusSlot.Fullscreen);
        if (IsSkipSegmentOffered)
            slots.Add(TvFocusSlot.SkipSegment);
        return slots;
    }

    private void MoveTvChromeFocus(int direction)
    {
        var visible = GetVisibleTvFocusSlots();
        if (visible.Count == 0)
            return;

        var currentSlot = IndexToTvFocusSlot(_tvChromeFocusIndex);
        var visibleIndex = visible.IndexOf(currentSlot);
        if (visibleIndex < 0)
            visibleIndex = visible.IndexOf(TvFocusSlot.Play);
        if (visibleIndex < 0)
            visibleIndex = 0;

        visibleIndex = (visibleIndex + direction + visible.Count) % visible.Count;
        SetTvChromeFocusSlot(visible[visibleIndex]);
        ResetHideTimer();
        NativeVideoDebug.Log("TvFocus slot=" + visible[visibleIndex] + " scrub=" + _seekScrubbing);
    }

    private bool TryMoveTvFocusToSkipSegment(bool up)
    {
        if (!IsSkipSegmentOffered)
            return false;

        var slot = IndexToTvFocusSlot(_tvChromeFocusIndex);
        if (up)
        {
            if (slot is TvFocusSlot.Play or TvFocusSlot.Volume or TvFocusSlot.SeekBar
                or TvFocusSlot.Settings or TvFocusSlot.Cast or TvFocusSlot.SyncPlay
                or TvFocusSlot.Fullscreen)
            {
                SetTvChromeFocusSlot(TvFocusSlot.SkipSegment);
                return true;
            }

            return false;
        }

        if (slot != TvFocusSlot.SkipSegment)
            return false;

        SetTvChromeFocusSlot(TvFocusSlot.Settings);
        return true;
    }

    private static TvFocusSlot IndexToTvFocusSlot(int index) => index switch
    {
        0 => TvFocusSlot.Back,
        1 => TvFocusSlot.Play,
        2 => TvFocusSlot.Volume,
        3 => TvFocusSlot.SeekBar,
        4 => TvFocusSlot.Settings,
        5 => TvFocusSlot.Cast,
        6 => TvFocusSlot.SyncPlay,
        7 => TvFocusSlot.Fullscreen,
        8 => TvFocusSlot.SkipSegment,
        _ => TvFocusSlot.Play
    };

    private static int TvFocusSlotToIndex(TvFocusSlot slot) => slot switch
    {
        TvFocusSlot.Back => 0,
        TvFocusSlot.Play => 1,
        TvFocusSlot.Volume => 2,
        TvFocusSlot.SeekBar => 3,
        TvFocusSlot.Settings => 4,
        TvFocusSlot.Cast => 5,
        TvFocusSlot.SyncPlay => 6,
        TvFocusSlot.Fullscreen => 7,
        TvFocusSlot.SkipSegment => 8,
        _ => 1
    };

    private void SetTvChromeFocusSlot(TvFocusSlot slot)
    {
        _tvChromeFocusIndex = TvFocusSlotToIndex(slot);
        _tvFocusOnSeekBar = slot == TvFocusSlot.SeekBar;
        ApplyTvChromeFocusHighlight();
    }

    private void ApplyTvChromeFocusHighlight()
    {
        ClearTvChromeFocus();
        if (_tvFocusOnSeekBar)
        {
            _seekBarFocusRing.Stroke = Colors.White;
            _seekBarFocusRing.BackgroundColor = TvFocusColor;
        }
        else
        {
            var slot = IndexToTvFocusSlot(_tvChromeFocusIndex);
            if (slot == TvFocusSlot.SkipSegment && IsSkipSegmentOffered)
            {
                _skipSegmentFocusRing.Stroke = Colors.White;
                _skipSegmentFocusRing.BackgroundColor = TvFocusColor;
            }
            else
            {
                var button = GetFocusedChromeButton();
                if (button is not null && button.IsVisible)
                    button.BackgroundColor = TvFocusColor;
            }
        }

        if (_skipSegmentHovered)
        {
            _skipSegmentFocusRing.Stroke = Colors.White;
            _skipSegmentFocusRing.BackgroundColor = TvFocusColor;
        }

        if (_hoveredChromeButton is not null && _hoveredChromeButton.IsVisible)
            _hoveredChromeButton.BackgroundColor = TvFocusColor;
    }

    private Button? GetFocusedChromeButton() => IndexToTvFocusSlot(_tvChromeFocusIndex) switch
    {
        TvFocusSlot.Back => _backButton,
        TvFocusSlot.Play => _playPauseButton,
        TvFocusSlot.Volume => _volumeButton,
        TvFocusSlot.Settings => _settingsButton,
        TvFocusSlot.Cast => _castButton,
        TvFocusSlot.SyncPlay => _syncPlayButton,
        TvFocusSlot.Fullscreen => _fullscreenButton,
        TvFocusSlot.SkipSegment => _skipSegmentButton,
        _ => null
    };

    private void ClearTvChromeFocus()
    {
        if (_backButton is not null)
            _backButton.BackgroundColor = Colors.Transparent;
        _playPauseButton.BackgroundColor = Colors.Transparent;
        _volumeButton.BackgroundColor = Colors.Transparent;
        _settingsButton.BackgroundColor = Colors.Transparent;
        _fullscreenButton.BackgroundColor = Colors.Transparent;
        _castButton.BackgroundColor = Colors.Transparent;
        _syncPlayButton.BackgroundColor = Colors.Transparent;
        _seekBarFocusRing.Stroke = Colors.Transparent;
        _seekBarFocusRing.BackgroundColor = Colors.Transparent;
        _skipSegmentFocusRing.Stroke = Colors.Transparent;
        _skipSegmentFocusRing.BackgroundColor = Colors.Transparent;
    }

    private void ActivateTvChromeFocus()
    {
        if (_tvFocusOnSeekBar)
        {
            BeginTvSeekEdit();
            return;
        }

        var button = GetFocusedChromeButton();
        if (button is null || !button.IsVisible)
        {
            TogglePlayPause();
            return;
        }

        if (ReferenceEquals(button, _playPauseButton))
            TogglePlayPause();
        else if (ReferenceEquals(button, _volumeButton))
            SetVolumeOpen(!_volumeOpen);
        else if (ReferenceEquals(button, _fullscreenButton))
            ToggleFullscreen();
        else if (ReferenceEquals(button, _settingsButton))
        {
            if (_settings.IsOpen)
                _settings.Close();
            else
                _settings.Open();
            ResetHideTimer(TimeSpan.FromSeconds(5));
        }
        else if (ReferenceEquals(button, _castButton))
            ToggleCastPanel();
        else if (ReferenceEquals(button, _syncPlayButton))
            ToggleSyncPlayPanel();
        else if (ReferenceEquals(button, _backButton))
            ClosePlayer();
        else if (ReferenceEquals(button, _skipSegmentButton))
            SkipActiveSegment();
        else
            TogglePlayPause();

        ResetHideTimer();
    }

    private void ResetHideTimer(TimeSpan? overrideTimeout = null)
    {
        StopHideTimer();
        if (IsPhoneOrTablet())
            return;
        if (_settings.IsOpen || _volumeOpen || _seekScrubbing)
            return;
        if (_awaitingFirstFrame)
            return;
        if (_startFailureVisible)
            return;

        var timeout = overrideTimeout
            ?? (_deviceType == DeviceType.TV ? OverlayTimeoutTv : OverlayTimeoutDesktop);
        _hideTimer = new Timer(timeout.TotalMilliseconds) { AutoReset = false };
        _hideTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(() => HideChrome());
        _hideTimer.Start();
    }

    private void StopHideTimer()
    {
        _hideTimer?.Stop();
        _hideTimer?.Dispose();
        _hideTimer = null;
    }

    private void TogglePlayPause()
    {
        if (_player.PlaybackState is PlaybackState.Playing or PlaybackState.Buffering)
            _player.Pause();
        else
            _player.Play();
        ResetHideTimer();
    }

    private void ToggleMute()
    {
        if (_player.IsMuted)
            _player.Unmute();
        else
            _player.Mute();
        UpdateTransport();
        ResetHideTimer();
    }

    private void ToggleFullscreen()
    {
        if (_player.IsFullScreen)
        {
            _player.IsFullScreen = false;
            _player.ExitFullScreen();
        }
        else
        {
            _player.IsFullScreen = true;
            _player.EnterFullScreen();
        }

        UpdateTransport();
        ResetHideTimer();
        if (_player.IsFullScreen)
            ResetCursorIdle();
        else
        {
            StopCursorIdle();
#if WINDOWS
            Platforms.Windows.WindowsIdleCursor.Show();
#endif
        }
    }

    private double DisplayedVolume =>
        _player.IsMuted ? 0 : _player.Volume;

    private void ApplyUserVolume(double volume01)
    {
        var next = Math.Clamp(volume01, 0, 1);
        // PlayerService is the shared source of truth across Direct (LibVLC) and HLS (Video.js).
        // On Windows, SupportsNativeVolume is false - never also write WASAPI (that made Direct quiet).
        _player.SetVolume(next);
        if (_volumeService?.SupportsNativeVolume == true)
            _volumeService.SetVolume(next);

        if (next <= 0.001)
            _player.Mute();
        else if (_player.IsMuted)
            _player.Unmute();
    }

    private void AdjustVolume(double delta)
    {
        var next = Math.Clamp(DisplayedVolume + delta, 0, 1);
        ApplyUserVolume(next);
        ShowHud($"{(int)(next * 100)}%", NativePlayerGlyphs.SpeakerHigh);
        UpdateTransport();
    }

    private void AttachVolumeHover()
    {
        if (!NativePointerInput.SupportsHoverRecognizers)
            return;

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) =>
        {
            if (!IsDesktopLike())
                return;

            CancelVolumeHoverHide();
            SetVolumeOpen(true);
        };
        pointer.PointerExited += (_, _) => ScheduleVolumeHoverHide();
        _volumeButton.GestureRecognizers.Add(pointer);
    }

    private void AttachVolumePopoverHover()
    {
        if (!NativePointerInput.SupportsHoverRecognizers)
            return;

        var pointer = new PointerGestureRecognizer();
        pointer.PointerEntered += (_, _) =>
        {
            if (!IsDesktopLike())
                return;

            CancelVolumeHoverHide();
        };
        pointer.PointerExited += (_, _) => ScheduleVolumeHoverHide();
        _volumePopover.GestureRecognizers.Add(pointer);
    }

    private void ScheduleVolumeHoverHide()
    {
        if (!IsDesktopLike() || _volumeSlider.IsDragging)
            return;

        CancelVolumeHoverHide();
        _volumeHoverHideTimer = new Timer(220) { AutoReset = false };
        _volumeHoverHideTimer.Elapsed += (_, _) =>
            MainThread.BeginInvokeOnMainThread(() => SetVolumeOpen(false));
        _volumeHoverHideTimer.Start();
    }

    private void CancelVolumeHoverHide()
    {
        _volumeHoverHideTimer?.Stop();
        _volumeHoverHideTimer?.Dispose();
        _volumeHoverHideTimer = null;
    }

    private void SetVolumeOpen(bool open)
    {
        if (!open)
            CancelVolumeHoverHide();

        _volumeOpen = open;
        _volumePopover.IsVisible = open && IsDesktopLike();
        if (open)
            StopHideTimer();
        else
            ResetHideTimer();
    }

    private void AccumulateSeek(double delta)
    {
        if (!_accumulateSeeking)
        {
            _accumulateSeeking = true;
            _seekBase = _player.CurrentTime;
            _seekOffset = 0;
        }

        _seekOffset += delta;
        var target = Math.Clamp(_seekBase + _seekOffset, 0, Math.Max(_player.Duration, 0));
        ShowHud(
            NativeTimeFormatting.Format(target),
            delta >= 0 ? NativePlayerGlyphs.Forward : NativePlayerGlyphs.Rewind);
        UpdateTimeLabel();

        _seekDebounceTimer?.Stop();
        _seekDebounceTimer?.Dispose();
        _seekDebounceTimer = new Timer(1000) { AutoReset = false };
        _seekDebounceTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(CommitAccumulateSeek);
        _seekDebounceTimer.Start();
    }

    private void CommitAccumulateSeek()
    {
        if (!_accumulateSeeking)
            return;

        var target = Math.Clamp(_seekBase + _seekOffset, 0, Math.Max(_player.Duration, 0));
        _accumulateSeeking = false;
        _seekOffset = 0;
        _player.Seek(target);
        UpdateTimeLabel();
    }

    private void TvScrub(int direction)
    {
        if (DateTime.UtcNow - _lastScrubUtc > TimeSpan.FromMilliseconds(400))
            _scrubRepeatCount = 0;
        _lastScrubUtc = DateTime.UtcNow;
        _scrubRepeatCount++;

        // Match Blazor SeekBar.GetScrubStep (hold acceleration).
        var step = GetScrubStepSeconds(_scrubRepeatCount) * direction;

        if (!_seekScrubbing)
        {
            if (IsNextEpisodeVisible)
                return;

            _seekBar.BeginEdit();
            _seekScrubbing = true;
            ShowChrome();
            SetTvChromeFocusSlot(TvFocusSlot.SeekBar);
            StopHideTimer();
        }

        _seekBar.ScrubBy(step);
        UpdateTimeLabel();
        UpdateSeekPreview(true);
        NativeVideoDebug.Log(
            "TvScrub dir=" + direction + " step=" + step + " preview=" + _seekBar.DisplayTime.ToString("F1")
            + "s repeat=" + _scrubRepeatCount);
    }

    private static double GetScrubStepSeconds(int scrubRepeatCount) => scrubRepeatCount switch
    {
        <= 4 => 2,
        <= 10 => 5,
        <= 18 => 10,
        <= 28 => 20,
        <= 40 => 30,
        _ => 60
    };

    private void ShowHud(string text, string icon)
    {
        NativeIconText.SetHud(_hudIconLabel, _hudTextLabel, icon, text);
        _hudBanner.IsVisible = true;
        _hudTimer?.Stop();
        _hudTimer?.Dispose();
        _hudTimer = new Timer(800) { AutoReset = false };
        _hudTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(() => _hudBanner.IsVisible = false);
        _hudTimer.Start();
    }

    private void OnDesktopPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!IsVisible || !IsDesktopLike() || _inputModalActive || IsNextEpisodeVisible)
            return;

#if WINDOWS
        Platforms.Windows.WindowsIdleCursor.Show();
#endif
        ResetCursorIdle();
        if (_showChrome)
            ResetHideTimer();
        else
            ShowChrome();
    }

    private void ResetCursorIdle()
    {
        StopCursorIdle();
        if (!_player.IsFullScreen)
            return;

        _cursorIdleTimer = new Timer(2000) { AutoReset = false };
        _cursorIdleTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!IsVisible || !_player.IsFullScreen)
                return;
#if WINDOWS
            Platforms.Windows.WindowsIdleCursor.Hide();
#endif
        });
        _cursorIdleTimer.Start();
    }

    private void StopCursorIdle()
    {
        _cursorIdleTimer?.Stop();
        _cursorIdleTimer?.Dispose();
        _cursorIdleTimer = null;
    }

    private void ClosePlayer()
    {
        if (_player.IsFullScreen)
        {
            _player.IsFullScreen = false;
            _player.ExitFullScreen();
        }

        HideChrome(force: true);
        RestoreBrightness();
        _progressTracker?.StopTracking();
        _player.Stop();
        _ = _player.HideAsync();
    }

    private void OnPlaybackEnded()
    {
        if (_handlingPlaybackEnded)
            return;

        _handlingPlaybackEnded = true;
        // Hide transport so TV/remote focus moves to the next-episode offer (Blazor SpatialNav
        // layer parity). Force-hide even if seek-scrub was still armed at Ended.
        StopDpadHold();
        HideChrome(force: true);
        _ = HandlePlaybackEndedAsync();
    }

    private async Task HandlePlaybackEndedAsync()
    {
        var mediaId = _player.Source?.MediaId;
        try
        {
            var offered = await TryLoadNextEpisodeOfferAsync();
            if (offered)
                return;

            if (!_player.IsVisible || _player.Source?.MediaId != mediaId)
                return;

            if (MainThread.IsMainThread)
                ClosePlayer();
            else
                await MainThread.InvokeOnMainThreadAsync(ClosePlayer);
        }
        finally
        {
            _handlingPlaybackEnded = false;
        }
    }

    private async Task LoadPreferencesAsync()
    {
        if (_prefs is null)
            return;

        try
        {
            _videoSettings = await _prefs.GetEffectiveVideoPlayerSettingsAsync();
            _player.ApplyVideoPlayerUxSettings(_videoSettings);
            _showChapterTicks = _videoSettings.ShowChapterTicks;
#if ANDROID
            K7.Clients.MAUI.Platforms.Android.AndroidSubtitleStyle.SetSettings(_videoSettings);
            TryApplyAndroidSubtitleStyle();
#elif WINDOWS
            VlcSubtitleStyle.SetSettings(_videoSettings);
            TryApplyWindowsSubtitleStyle();
#endif
            ApplySidecarSubtitleStyle();

            RefreshSeekChapters();
        }
        catch
        {
            // Best-effort prefs.
        }

        ApplySkipSegmentOnMainThread();

        if (_deviceStorage is not null)
        {
            try
            {
                _nepBehavior = _deviceStorage.Get(PreferenceKeys.NEXT_EPISODE_BEHAVIOR, "AutoPlay") ?? "AutoPlay";
            }
            catch
            {
                _nepBehavior = "AutoPlay";
            }
        }
    }

#if ANDROID || WINDOWS
#if ANDROID
    private void TryApplyAndroidSubtitleStyle()
    {
        MainThread.BeginInvokeOnMainThread(() => FindBlazorPage()?.ApplyPendingAndroidSubtitleStyle());
    }
#endif
#if WINDOWS
    private void TryApplyWindowsSubtitleStyle()
    {
        MainThread.BeginInvokeOnMainThread(() => FindBlazorPage()?.ApplyPendingWindowsSubtitleStyle());
    }
#endif

    private void NotifySidecarTextReady(bool ready) =>
        FindBlazorPage()?.NotifySidecarTextSubtitles(ready);

    private BlazorPage? FindBlazorPage()
    {
        Element? current = this;
        while (current is not null)
        {
            if (current is BlazorPage page)
                return page;
            current = current.Parent;
        }

        return null;
    }
#endif

    private async Task RefreshSegmentsAsync()
    {
        var mediaId = _player.Source?.MediaId;
        if (_mediaService is null || mediaId is null)
        {
            ResetSkipSession();
            _segments = null;
            _segmentsMediaId = null;
            ApplySkipSegmentOnMainThread();
            return;
        }

        var mediaChanged = mediaId != _segmentsMediaId;
        if (mediaChanged)
        {
            ResetSkipSession();
            // Drop the previous episode's windows immediately so AutoSkip cannot seek
            // to a stale intro/outro end while the new list is in flight.
            _segments = null;
            _segmentsMediaId = null;
        }

        if (mediaChanged || _segments is null)
        {
            try
            {
                _segments = await _mediaService.GetMediaSegmentsAsync(mediaId.Value);
                _segmentsMediaId = mediaId;
                RefreshSeekChapters();
            }
            catch
            {
                _segments = null;
            }
        }

        ApplySkipSegmentOnMainThread();
    }

    private void ResetSkipSession()
    {
        _skipState = default;
        SetSkipSegmentVisible(false);
    }

    private void ApplySkipSegmentOnMainThread()
    {
        if (MainThread.IsMainThread)
            ApplySkipSegmentAtCurrentTime();
        else
            MainThread.BeginInvokeOnMainThread(ApplySkipSegmentAtCurrentTime);
    }

    private void ApplySkipSegmentAtCurrentTime() => UpdateSkipSegment(_player.CurrentTime);

    private void RefreshSeekChapters()
    {
        var markers = SeekBarChapterBuilder.Build(
            _showChapterTicks,
            _player.Source?.Chapters,
            _segments,
            NativeStrings.Intro,
            NativeStrings.Outro);
        _seekBar.Chapters = markers;
        _seekBar.Refresh();
    }

    /// <summary>Mirrors SkipSegmentOverlay via <see cref="SkipSegmentPresenter"/>.</summary>
    private void UpdateSkipSegment(double time)
    {
        var result = SkipSegmentPresenter.Tick(
            _skipState,
            _segments,
            _videoSettings,
            time,
            IsChromeVisible,
            DateTime.UtcNow);
        _skipState = result.State;

        if (result.Action == SkipSegmentPresenter.ActionKind.AutoSkip
            && result.State.ActiveSegment is { } autoSegment)
        {
            NativeVideoDebug.Log("SkipSegment auto type=" + autoSegment.Type + " endMs=" + autoSegment.EndMs);
            SkipActiveSegment(autoSegment);
            return;
        }

        if (result.State.Visible && result.State.ActiveSegment is { } active)
        {
            _skipSegmentButton.Text = active.Type == MediaSegmentType.Intro
                ? NativeStrings.SkipIntro
                : NativeStrings.SkipOutro;
            SetSkipSegmentVisible(true);
        }
        else
        {
            SetSkipSegmentVisible(false);
        }
    }

    private void SetSkipSegmentVisible(bool visible)
    {
        var wasVisible = _skipSegmentFocusRing.IsVisible;
        _skipSegmentButton.IsVisible = visible;
        _skipSegmentFocusRing.IsVisible = visible;
        if (wasVisible == visible)
            return;

        SyncTvSurfaceComposition();

        if (visible)
            TryFocusSkipSegmentIfOffered();
        else if (IndexToTvFocusSlot(_tvChromeFocusIndex) == TvFocusSlot.SkipSegment)
            SetTvChromeFocusSlot(TvFocusSlot.Play);
    }

    private void TryFocusSkipSegmentIfOffered()
    {
        if (!_showChrome || !IsSkipSegmentOffered || _seekScrubbing)
            return;
        if (_settings.IsOpen || _castPanelOpen || _syncPlayPanelOpen)
            return;

        SetTvChromeFocusSlot(TvFocusSlot.SkipSegment);
    }

    private void SkipActiveSegment(MediaSegmentDto? segment = null)
    {
        segment ??= _skipState.ActiveSegment;
        if (segment is null)
            return;

        var segmentType = segment.Type;
        var endSeconds = segment.EndMs / 1000.0;
        _skipState = _skipState with
        {
            Visible = false,
            ActiveSegment = null,
            LastSkipUtc = DateTime.UtcNow
        };
        _player.Seek(endSeconds);
        SetSkipSegmentVisible(false);
        ShowSkippedNotification(segmentType);
        NativeVideoDebug.Log("SkipSegment seek type=" + segmentType + " to=" + endSeconds.ToString("F1") + "s");
    }

    private void ShowSkippedNotification(MediaSegmentType type)
    {
        NativeIconText.SetHud(
            _skipNotificationIconLabel,
            _skipNotificationTextLabel,
            NativePlayerGlyphs.SkipForward,
            type == MediaSegmentType.Intro ? NativeStrings.IntroSkipped : NativeStrings.OutroSkipped);
        _skipNotificationBanner.IsVisible = true;
        StopSkipNotificationTimer();
        _skipNotificationTimer = new Timer(3000) { AutoReset = false };
        _skipNotificationTimer.Elapsed += (_, _) =>
            MainThread.BeginInvokeOnMainThread(() => _skipNotificationBanner.IsVisible = false);
        _skipNotificationTimer.Start();
    }

    private void StopSkipNotificationTimer()
    {
        _skipNotificationTimer?.Stop();
        _skipNotificationTimer?.Dispose();
        _skipNotificationTimer = null;
    }

    private bool IsDesktopLike() => _deviceType is DeviceType.Desktop or DeviceType.Unknown;
    private bool IsPhoneOrTablet() => _deviceType is DeviceType.Phone or DeviceType.Tablet;

    private void ConfigureGestureCatcher(BoxView catcher, bool panSideLeft)
    {
        // Fully transparent: #01000000 blocks TextureView/SurfaceView on Amlogic TV GPUs
        // (decoded frames keep counting, picture stays on the first composite).
        catcher.Color = Colors.Transparent;
        catcher.InputTransparent = false;

        var tap = new TapGestureRecognizer { NumberOfTapsRequired = 1 };
        tap.Tapped += OnBackgroundTapped;
        catcher.GestureRecognizers.Add(tap);

        var doubleTap = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        // Left catcher => skip back; right catcher => skip forward.
        doubleTap.Tapped += (_, _) => OnDoubleTapped(isRight: !panSideLeft);
        catcher.GestureRecognizers.Add(doubleTap);

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += (_, e) => OnPanUpdated(e, panSideLeft);
        catcher.GestureRecognizers.Add(pan);
    }

    private void SetGestureCatchersInputTransparent(bool value)
    {
        _leftCatcher.InputTransparent = value;
        _rightCatcher.InputTransparent = value;
    }
}
