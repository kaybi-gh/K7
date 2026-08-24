using System.Timers;
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
using K7.Shared.Interfaces;
using Microsoft.Maui.Controls.Shapes;
using DeviceType = K7.Server.Domain.Enums.DeviceType;
using MediaSegmentType = K7.Shared.Enums.MediaSegmentType;
using Timer = System.Timers.Timer;

namespace K7.Clients.MAUI.Controls.Video;

/// <summary>
/// Full native video chrome for MAUI play sessions (Android/iOS only - Windows stays Blazor +
/// Video.js). Mirrors <c>VideoPlayerControlsOverlay.razor(.cs)</c> 1:1: transport, seek bar with
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
    private readonly Grid _topBar = new();
    private readonly Grid _bottomBar = new();
    private readonly Label _titleLabel = new();
    private readonly Label _timeLabel = new();
    private int _tvChromeFocusIndex;
    private bool _tvFocusOnSeekBar;
    private static readonly Color TvFocusColor = Color.FromArgb("#66FFFFFF");
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
        SeekBar,
        Settings,
        Cast,
        SyncPlay,
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
    private readonly Slider _volumeSlider = new();
    private readonly Border _volumePopover = new();
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
        SizeChanged += (_, _) => UpdateSettingsAvailableHeight();
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
    }

    public void Detach()
    {
        StopDpadHold();
        DisposeSeekPreview();
        UnsubscribePlayer();
        UnsubscribeSyncPlay();
        StopHideTimer();
        StopSkipNotificationTimer();
        _settings.Close();
        _volumeOpen = false;
        _volumePopover.IsVisible = false;
        SetCastPanelOpen(false);
        SetSyncPlayPanelOpen(false);
        DismissNextEpisode();
        RestoreBrightness();
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
            IsVisible = active;
            NativeVideoDebug.Log("SetActive active=" + active + " device=" + _deviceType);
            if (active)
            {
                Attach();
                _awaitingFirstFrame = true;
                SetLoadingVeil(true);
                // Warm settings UI while the veil is up so the first Open does not hitch ExoPlayer.
                try { _settings.Rebuild(); } catch { /* ignore */ }
                // TV: start with chrome hidden so the first OK reveals it with focus,
                // matching Blazor auto-hide behavior after play begins.
                if (_deviceType == DeviceType.TV)
                    HideChrome();
                else
                    ShowChrome();
                ResetSkipSession();
                _ = RefreshSegmentsAsync();
                RefreshSeekChapters();
                UpdateTransport();
                WarmSeekThumbnails();
            }
            else
            {
                SetLoadingVeil(false);
                Detach();
            }
        });
    }

    private bool _awaitingFirstFrame = true;

    /// <summary>Black cover until the first decoded frame (avoids TextureView white flash).</summary>
    public void SetLoadingVeil(bool loading)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Ignore mid-play buffer/seek requests once a frame has been shown.
            if (loading && !_awaitingFirstFrame)
                return;

            _loadingVeil.IsVisible = loading;
            NativeVideoDebug.Log("SetLoadingVeil loading=" + loading);
            if (!loading && _deviceType == DeviceType.TV && !_showChrome)
                ResetHideTimer();
        });
    }

    /// <summary>First Playing frame - drop the startup veil and allow seek without black cover.</summary>
    public void NotifyFirstFrameReady()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _awaitingFirstFrame = false;
            _loadingVeil.IsVisible = false;
            NativeVideoDebug.Log("SetLoadingVeil loading=False firstFrame");
            if (_deviceType == DeviceType.TV && !_showChrome)
                ResetHideTimer();
        });
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
                StopDpadHold();
                return HandleBack();
            }

            return HandleNextEpisodeKey(key, isKeyUp);
        }

        // Back always goes through HandleBack.
        if (key is "escape" or "browserback" or "goback" or "back")
        {
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
            TogglePlayPause();
            return true;
        }

        if (VideoRemoteTransportKeys.IsOverlaySkip(key))
            return HandleMediaSkipKey(key, VideoRemoteTransportKeys.IsOverlaySkipBack(key), isKeyUp);

        if (key is "m" && IsDesktopLike())
        {
            ToggleMute();
            return true;
        }

        if (key is "f" && IsDesktopLike())
        {
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

        _loadingVeil.Color = Colors.Black;
        _loadingVeil.InputTransparent = true;
        Children.Add(_loadingVeil);

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

        _skipSegmentButton.Text = NativeStrings.SkipIntro;
        _skipSegmentButton.BackgroundColor = Color.FromArgb("#CCFFFFFF");
        _skipSegmentButton.TextColor = Colors.Black;
        _skipSegmentButton.Padding = new Thickness(16, 10);
        _skipSegmentButton.CornerRadius = 8;
        _skipSegmentButton.HorizontalOptions = LayoutOptions.Center;
        _skipSegmentButton.Clicked += (_, _) => SkipActiveSegment();
        DisablePlatformFocus(_skipSegmentButton);
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
        _volumeButton.Clicked += (_, _) =>
        {
            if (IsDesktopLike())
                SetVolumeOpen(!_volumeOpen);
            else
                ToggleMute();
        };
        left.Children.Add(_volumeButton);

        _volumeSlider.Minimum = 0;
        _volumeSlider.Maximum = 1;
        _volumeSlider.Value = _player.Volume;
        _volumeSlider.WidthRequest = 36;
        _volumeSlider.HeightRequest = 140;
        _volumeSlider.ValueChanged += (_, e) =>
        {
            _player.SetVolume(e.NewValue);
            if (e.NewValue <= 0.001 && !_player.IsMuted)
                _player.Mute();
            else if (e.NewValue > 0.001 && _player.IsMuted)
                _player.Unmute();
            UpdateTransport();
            ResetHideTimer();
        };

        _volumePopover.Content = _volumeSlider;
        _volumePopover.BackgroundColor = Color.FromArgb("#EE121212");
        _volumePopover.Padding = new Thickness(8);
        _volumePopover.StrokeShape = new RoundRectangle { CornerRadius = 10 };
        _volumePopover.IsVisible = false;
        _volumePopover.HorizontalOptions = LayoutOptions.Start;
        _volumePopover.VerticalOptions = LayoutOptions.End;
        _volumePopover.Margin = new Thickness(56, 0, 0, 72);
        Children.Add(_volumePopover);

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

    private static void StyleTransportButton(Button button)
    {
        button.BackgroundColor = Colors.Transparent;
        button.TextColor = Colors.White;
        button.FontFamily = NativePlayerGlyphs.FontFamily;
        button.FontSize = 20;
        button.Padding = new Thickness(10, 6);
        button.FontAutoScalingEnabled = false;
        // TV uses software focus rings; native Button focus steals DPAD from next-episode.
        DisablePlatformFocus(button);
    }

    private static Button CreateIconButton(string glyph)
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
        }

        Apply();
        element.HandlerChanged += (_, _) => Apply();
    }

    private void WireEvents()
    {
        _settings.OpenedChanged += (_, _) =>
        {
            UpdateSettingsAvailableHeight();
            UpdateChromeVisibility();
            ResetHideTimer(TimeSpan.FromSeconds(5));
        };
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
        _player.SubtitleTrackChanged += _ =>
        {
            if (_settings.IsOpen)
                MainThread.BeginInvokeOnMainThread(_settings.Rebuild);
        };
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
#endif
        RefreshSeekChapters();
        ApplySkipSegmentOnMainThread();
    }

    private void OnBackPressed() => MainThread.BeginInvokeOnMainThread(() => HandleBack());

    private void OnPlayerChanged(PlaybackState state) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateTransport();
            if (state == PlaybackState.Ended)
                OnPlaybackEnded();
            else if (state == PlaybackState.Playing && IsNextEpisodeVisible)
                DismissNextEpisode();
        });

    private void OnMutedChanged(bool _) => MainThread.BeginInvokeOnMainThread(UpdateTransport);
    private void OnVolumeChanged(double _) => MainThread.BeginInvokeOnMainThread(UpdateTransport);

    private void OnTimeChanged(double time) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            UpdateTimeLabel();
            _seekBar.Refresh();
            UpdateSkipSegment(time);
        });

    private void OnBufferedChanged(double _) => MainThread.BeginInvokeOnMainThread(_seekBar.Refresh);

    private void OnSourceChanged(PlayerSource source) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            DisposeSpriteBitmap();
            _titleLabel.Text = source.Title ?? string.Empty;
            _ = RefreshSegmentsAsync();
            RefreshSeekChapters();
            UpdateTransport();
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
    }

    private void UpdateTransport()
    {
        var playing = _player.PlaybackState is PlaybackState.Playing or PlaybackState.Buffering;
        _playPauseButton.Text = playing ? NativePlayerGlyphs.Pause : NativePlayerGlyphs.Play;
        _volumeButton.Text = _player.IsMuted || _player.Volume <= 0.001
            ? NativePlayerGlyphs.SpeakerMuted
            : _player.Volume < 0.4 ? NativePlayerGlyphs.SpeakerLow : NativePlayerGlyphs.SpeakerHigh;
        _volumeSlider.Value = _player.IsMuted ? 0 : _player.Volume;
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

        if (force && _seekScrubbing)
            CancelTvScrub();

        _showChrome = false;
        SetVolumeOpen(false);
        ClearTvChromeFocus();
        _suppressShowUntil = DateTime.UtcNow.AddMilliseconds(500);
        UpdateChromeVisibility();
        StopHideTimer();
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
        var slots = new List<TvFocusSlot> { TvFocusSlot.Back, TvFocusSlot.Play, TvFocusSlot.SeekBar, TvFocusSlot.Settings };
        if (IsSkipSegmentOffered)
            slots.Add(TvFocusSlot.SkipSegment);
        if (_castButton.IsVisible)
            slots.Add(TvFocusSlot.Cast);
        if (_syncPlayButton.IsVisible)
            slots.Add(TvFocusSlot.SyncPlay);
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
            if (slot is TvFocusSlot.Play or TvFocusSlot.SeekBar or TvFocusSlot.Settings
                or TvFocusSlot.Cast or TvFocusSlot.SyncPlay)
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
        2 => TvFocusSlot.SeekBar,
        3 => TvFocusSlot.Settings,
        4 => TvFocusSlot.Cast,
        5 => TvFocusSlot.SyncPlay,
        6 => TvFocusSlot.SkipSegment,
        _ => TvFocusSlot.Play
    };

    private static int TvFocusSlotToIndex(TvFocusSlot slot) => slot switch
    {
        TvFocusSlot.Back => 0,
        TvFocusSlot.Play => 1,
        TvFocusSlot.SeekBar => 2,
        TvFocusSlot.Settings => 3,
        TvFocusSlot.Cast => 4,
        TvFocusSlot.SyncPlay => 5,
        TvFocusSlot.SkipSegment => 6,
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
            return;
        }

        if (IndexToTvFocusSlot(_tvChromeFocusIndex) == TvFocusSlot.SkipSegment && IsSkipSegmentOffered)
        {
            _skipSegmentFocusRing.Stroke = Colors.White;
            _skipSegmentFocusRing.BackgroundColor = TvFocusColor;
            return;
        }

        var button = GetFocusedChromeButton();
        if (button is null || !button.IsVisible)
            return;

        button.BackgroundColor = TvFocusColor;
    }

    private Button? GetFocusedChromeButton() => IndexToTvFocusSlot(_tvChromeFocusIndex) switch
    {
        TvFocusSlot.Back => _backButton,
        TvFocusSlot.Play => _playPauseButton,
        TvFocusSlot.Settings => _settingsButton,
        TvFocusSlot.Cast => _castButton,
        TvFocusSlot.SyncPlay => _syncPlayButton,
        TvFocusSlot.SkipSegment => _skipSegmentButton,
        _ => null
    };

    private void ClearTvChromeFocus()
    {
        if (_backButton is not null)
            _backButton.BackgroundColor = Colors.Transparent;
        _playPauseButton.BackgroundColor = Colors.Transparent;
        _settingsButton.BackgroundColor = Colors.Transparent;
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
            _player.ExitFullScreen();
        else
            _player.EnterFullScreen();
        ResetHideTimer();
    }

    private void AdjustVolume(double delta)
    {
        var next = Math.Clamp((_player.IsMuted ? 0 : _player.Volume) + delta, 0, 1);
        _player.SetVolume(next);
        if (next <= 0)
            _player.Mute();
        else if (_player.IsMuted)
            _player.Unmute();
        ShowHud($"{(int)(next * 100)}%", NativePlayerGlyphs.SpeakerHigh);
        UpdateTransport();
    }

    private void SetVolumeOpen(bool open)
    {
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

    private void ClosePlayer()
    {
        HideChrome();
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
#endif

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

#if ANDROID
    private void TryApplyAndroidSubtitleStyle()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            Element? current = this;
            while (current is not null)
            {
                if (current is BlazorPage page)
                {
                    page.ApplyPendingAndroidSubtitleStyle();
                    return;
                }

                current = current.Parent;
            }
        });
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
        catcher.Color = Color.FromArgb("#01000000");
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
