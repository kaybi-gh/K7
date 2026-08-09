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
using IntroSkipBehavior = K7.Shared.Enums.IntroSkipBehavior;
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
        SyncPlay
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
    private readonly Slider _volumeSlider = new();
    private readonly Border _volumePopover = new();
    private readonly NativeSeekBar _seekBar;
    private readonly NativePlaybackSettingsPanel _settings;
    private readonly BoxView _tapCatcher = new();

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
    private static readonly TimeSpan DpadHoldScrubDelay = TimeSpan.FromMilliseconds(320);
    private static readonly TimeSpan DpadHoldInterval = TimeSpan.FromMilliseconds(110);
    private IReadOnlyList<MediaSegmentDto>? _segments;
    private MediaSegmentDto? _activeSegment;
    private bool _autoSkippedActiveSegment;
    private bool _skipDismissed;
    private DateTime _skipShowTimeUtc;
    private DateTime _lastSkipUtc = DateTime.MinValue;
    private VideoPlayerSettingsDto? _videoSettings;
    private bool _showChapterTicks = true;
    private Guid? _segmentsMediaId;

    private static readonly TimeSpan OverlayTimeoutDesktop = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan OverlayTimeoutTv = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SkipCooldown = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan SkipButtonDisplayDuration = TimeSpan.FromSeconds(5);

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

        BuildLayout();
        WireEvents();
        SizeChanged += (_, _) => UpdateSettingsAvailableHeight();
    }

    public bool IsChromeVisible =>
        _showChrome || _settings.IsOpen || _volumeOpen || _seekScrubbing || _castPanelOpen || _syncPlayPanelOpen;

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
            NativeVideoDebug.Log("HandleKey key=" + key + " chrome=" + _showChrome + " scrub=" + _seekScrubbing + " device=" + _deviceType);

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

        if (IsNextEpisodeVisible)
            return HandleNextEpisodeKey(key, isKeyUp);

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
            if (!isKeyUp && (up || down) && IsDesktopLike())
            {
                AdjustVolume(up ? 0.1 : -0.1);
                return true;
            }

            if (!isKeyUp && select)
            {
                if (_skipSegmentButton.IsVisible && _activeSegment is not null)
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
        if (_dpadHoldKey is null || !_dpadHoldScrubArmed)
            return;

        if (_dpadHoldKey is "dpad_left" or "dpad_right")
        {
            if (!_showChrome || _seekScrubbing)
                TvScrub(_dpadHoldKey == "dpad_left" ? -1 : 1);
            else
                StopDpadHold();
        }
    }

    private void SkipByPreference(bool backward)
    {
        var delta = backward ? -_player.SkipBackSeconds : _player.SkipForwardSeconds;
        if (Math.Abs(delta) < 0.1)
            delta = backward ? -10 : 10;

        var duration = Math.Max(_player.Duration, 0);
        var target = Math.Clamp(_player.CurrentTime + delta, 0, duration > 0 ? duration : double.MaxValue);
        _player.Seek(target);
        var seconds = (int)Math.Round(Math.Abs((double)delta));
        var label = (delta >= 0 ? "+" : "-") + seconds + " s";
        ShowHud(label, delta >= 0 ? NativePlayerGlyphs.Forward : NativePlayerGlyphs.Rewind);
        UpdateTimeLabel();
        NativeVideoDebug.Log("SkipByPreference delta=" + delta.ToString("F0") + "s target=" + target.ToString("F1") + "s");
    }

    private void BuildLayout()
    {
        _tapCatcher.Color = Color.FromArgb("#01000000");
        _tapCatcher.InputTransparent = false;
        var tap = new TapGestureRecognizer();
        tap.Tapped += OnBackgroundTapped;
        tap.NumberOfTapsRequired = 1;
        _tapCatcher.GestureRecognizers.Add(tap);

        var doubleTap = new TapGestureRecognizer { NumberOfTapsRequired = 2 };
        doubleTap.Tapped += OnDoubleTapped;
        _tapCatcher.GestureRecognizers.Add(doubleTap);

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        _tapCatcher.GestureRecognizers.Add(pan);

        var pointer = new PointerGestureRecognizer();
        pointer.PointerPressed += OnPointerPressed;
        _tapCatcher.GestureRecognizers.Add(pointer);
        Children.Add(_tapCatcher);

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
        _chrome.InputTransparent = true;

        BuildTopBar();
        BuildBottomBar();
        Grid.SetRow(_topBar, 0);
        Grid.SetRow(_bottomBar, 2);
        _chrome.Children.Add(_topBar);
        _chrome.Children.Add(_bottomBar);
        // Make interactive children receive input
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
        _skipSegmentButton.IsVisible = false;
        _skipSegmentButton.HorizontalOptions = LayoutOptions.End;
        _skipSegmentButton.VerticalOptions = LayoutOptions.End;
        _skipSegmentButton.Margin = new Thickness(0, 0, 24, 120);
        _skipSegmentButton.BackgroundColor = Color.FromArgb("#CCFFFFFF");
        _skipSegmentButton.TextColor = Colors.Black;
        _skipSegmentButton.Padding = new Thickness(16, 10);
        _skipSegmentButton.Clicked += (_, _) => SkipActiveSegment();
        Children.Add(_skipSegmentButton);

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
    }

    private static Button CreateIconButton(string glyph)
    {
        var button = new Button { Text = glyph };
        StyleTransportButton(button);
        return button;
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
        if (_settings.IsOpen)
        {
            _settings.Close();
            return;
        }

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
        _showChrome = true;
        UpdateChromeVisibility();
        ResetHideTimer();
    }

    private void ShowChromeWithTvFocus()
    {
        ShowChrome();
        // Default TV focus on play/pause (Blazor SpatialNav.FocusFirstAsync(".play-pause-btn")).
        SetTvChromeFocusSlot(TvFocusSlot.Play);
    }

    private void HideChrome()
    {
        if (_settings.IsOpen || _seekScrubbing)
            return;

        _showChrome = false;
        SetVolumeOpen(false);
        ClearTvChromeFocus();
        _suppressShowUntil = DateTime.UtcNow.AddMilliseconds(500);
        UpdateChromeVisibility();
        StopHideTimer();
    }

    private List<TvFocusSlot> GetVisibleTvFocusSlots()
    {
        var slots = new List<TvFocusSlot> { TvFocusSlot.Back, TvFocusSlot.Play, TvFocusSlot.SeekBar, TvFocusSlot.Settings };
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

    private static TvFocusSlot IndexToTvFocusSlot(int index) => index switch
    {
        0 => TvFocusSlot.Back,
        1 => TvFocusSlot.Play,
        2 => TvFocusSlot.SeekBar,
        3 => TvFocusSlot.Settings,
        4 => TvFocusSlot.Cast,
        5 => TvFocusSlot.SyncPlay,
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
        _hideTimer.Elapsed += (_, _) => MainThread.BeginInvokeOnMainThread(HideChrome);
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
        _player.Stop();
        _ = _player.HideAsync();
    }

    private void OnPlaybackEnded()
    {
        ShowChrome();
        _ = LoadNextEpisodeOfferAsync();
    }

    private async Task LoadPreferencesAsync()
    {
        if (_prefs is null)
            return;

        try
        {
            _videoSettings = await _prefs.GetEffectiveVideoPlayerSettingsAsync();
            _showChapterTicks = _videoSettings.ShowChapterTicks;
            _player.SetSkipBackSeconds(_videoSettings.SkipBackSeconds);
            _player.SetSkipForwardSeconds(_videoSettings.SkipForwardSeconds);

            RefreshSeekChapters();
        }
        catch
        {
            // Best-effort prefs.
        }

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

    private async Task RefreshSegmentsAsync()
    {
        var mediaId = _player.Source?.MediaId;
        if (_mediaService is null || mediaId is null || mediaId == _segmentsMediaId)
            return;

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

    /// <summary>Mirrors SkipSegmentOverlay.OnTimeChanged: 3s post-skip cooldown, and the
    /// show-button variant dismisses after 5s once chrome is hidden (persists while visible).</summary>
    private void UpdateSkipSegment(double time)
    {
        if (_segments is null || _segments.Count == 0 || _videoSettings is null)
        {
            _skipSegmentButton.IsVisible = false;
            _activeSegment = null;
            return;
        }

        var timeMs = time * 1000.0;
        var next = _segments.FirstOrDefault(s =>
            s.Type is MediaSegmentType.Intro or MediaSegmentType.Outro
            && timeMs >= s.StartMs
            && timeMs < s.EndMs);

        if (!ReferenceEquals(next, _activeSegment))
        {
            _activeSegment = next;
            _autoSkippedActiveSegment = false;
            _skipDismissed = false;
        }

        if (_activeSegment is null)
        {
            _skipSegmentButton.IsVisible = false;
            return;
        }

        var behavior = _activeSegment.Type == MediaSegmentType.Intro
            ? _videoSettings.IntroSkipBehavior
            : _videoSettings.OutroSkipBehavior;

        if (behavior == IntroSkipBehavior.Disabled)
        {
            _skipSegmentButton.IsVisible = false;
            return;
        }

        var inCooldown = DateTime.UtcNow - _lastSkipUtc < SkipCooldown;

        if (behavior == IntroSkipBehavior.AutoSkip)
        {
            if (!_autoSkippedActiveSegment && !inCooldown)
            {
                _autoSkippedActiveSegment = true;
                SkipActiveSegment();
            }

            return;
        }

        // ShowButton: persists while chrome is visible; auto-dismisses 5s after first shown
        // once chrome is hidden (matches SkipSegmentOverlay's ControlsVisible gate).
        if (!_skipDismissed || IsChromeVisible)
        {
            if (!_skipSegmentButton.IsVisible)
                _skipShowTimeUtc = DateTime.UtcNow;
            else if (!IsChromeVisible && DateTime.UtcNow - _skipShowTimeUtc >= SkipButtonDisplayDuration)
            {
                _skipSegmentButton.IsVisible = false;
                _skipDismissed = true;
                return;
            }

            _skipSegmentButton.Text = _activeSegment.Type == MediaSegmentType.Intro
                ? NativeStrings.SkipIntro
                : NativeStrings.SkipOutro;
            _skipSegmentButton.IsVisible = true;
        }
        else
        {
            _skipSegmentButton.IsVisible = false;
        }
    }

    private void SkipActiveSegment()
    {
        if (_activeSegment is null)
            return;

        var segmentType = _activeSegment.Type;
        var endSeconds = _activeSegment.EndMs / 1000.0;
        _lastSkipUtc = DateTime.UtcNow;
        _player.Seek(endSeconds);
        _skipSegmentButton.IsVisible = false;
        _activeSegment = null;
        ShowSkippedNotification(segmentType);
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
}
