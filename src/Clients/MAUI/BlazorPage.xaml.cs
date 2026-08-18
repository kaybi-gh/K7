using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using K7.Clients.MAUI.Controls.Video;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using System.ComponentModel;
#if ANDROID
using K7.Clients.MAUI.Platforms.Android;
#endif

namespace K7.Clients.MAUI;

public partial class BlazorPage : ContentPage
{
    private readonly IPlayerService _playerService;
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly BackButtonService _backButtonService;
    private readonly IK7ServerService _k7ServerService;

    private static readonly string DownloadsBasePath = Path.GetFullPath(Path.Combine(FileSystem.AppDataDirectory, "downloads"));
    private static readonly string DownloadsBasePathPrefix = DownloadsBasePath.EndsWith(Path.DirectorySeparatorChar)
        ? DownloadsBasePath
        : DownloadsBasePath + Path.DirectorySeparatorChar;

    private bool _eventsDetached;
#if !ANDROID && !IOS && !WINDOWS
    private CancellationTokenSource? _audioFadeCts;
    private bool _audioCrossfadeInProgress;
    private string? _audioGaplessPrebufferedUrl;
    private double _audioLoudnessLinearGain = 1.0;
    /// <summary>
    /// When true, NativeAudioCrossfadePlayer is the active output (after a crossfade promote).
    /// Matches Web audioplayer.js element swap so we never reload the incoming track from 0.
    /// </summary>
    private bool _audioRolesSwapped;
    private bool _audioSuppressMediaFailed;

    private MediaElement ActiveAudioPlayer =>
        _audioRolesSwapped ? NativeAudioCrossfadePlayer : NativeAudioPlayer;

    private MediaElement IdleAudioPlayer =>
        _audioRolesSwapped ? NativeAudioPlayer : NativeAudioCrossfadePlayer;
#endif
#if !WINDOWS
    // MediaFailed can flap on Source swaps; report once per distinct failure within the window.
    private DateTime _lastMediaFailedReportUtc = DateTime.MinValue;
    private string? _lastMediaFailedReportKey;
    private static readonly TimeSpan MediaFailedReportDedupeWindow = TimeSpan.FromSeconds(30);
    private int _nativeAuthRecoveryCount;
    private DateTime _lastNativeAuthRecoveryUtc = DateTime.MinValue;
    private bool _openingNativeSource;
    private double? _authRebindResumeOverride;
    private ICustomAuthenticationStateProvider? _authStateProvider;
    private bool _accessTokenChangedSubscribed;

    private static readonly HashSet<string> SensitiveQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "token",
        "access_token",
        "ephemeral_token",
        "refresh_token",
        "authorization",
        "api_key",
        "apikey",
        "key",
        "sig",
        "signature"
    };
#endif

    public BlazorPage(
        IPlayerService playerService,
        IAudioPlayerService audioPlayerService,
        BackButtonService backButtonService,
        IK7ServerService k7ServerService)
    {
        _playerService = playerService;
        _audioPlayerService = audioPlayerService;
        _backButtonService = backButtonService;
        _k7ServerService = k7ServerService;
        InitializeComponent();
        var startPath = ResolveStartPath();
        blazorWebView.StartPath = startPath;
#if WINDOWS
        SyncWindowsStreamAuthContext();
#endif
        blazorWebView.WebResourceRequested += OnWebResourceRequested;
#if ANDROID
        if (AndroidStartupLottieOverlay.IsShown)
        {
            SplashAnimation.IsVisible = false;
            SplashLogo.IsVisible = false;
        }
#endif
        InitializeSplashOverlay();
        InitializePlayer();
        InitializeAudioPlayer();
        InitializeNativeVideoOverlay();
        Loaded += OnBlazorPageLoaded;
    }

    private static string ResolveStartPath()
    {
        var services = IPlatformApplication.Current?.Services;
        var localUsers = services?.GetService<ILocalUserService>();
        if (services is null || localUsers is null)
            return MauiBlazorStartPath.SelectProfile;

        var isTv = services.GetService<IDeviceService>()?.CachedDeviceType == Server.Domain.Enums.DeviceType.TV;
        var guestEnabled = services.GetService<IDeviceStorageService>() is { } storage
            ? CachedGuestAccess.TryGetEnabled(storage)
            : null;

        return MauiBlazorStartPath.Resolve(localUsers, isTv, guestEnabled);
    }

    private void OnBlazorPageLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnBlazorPageLoaded;
        InitializeNativeVideoOverlay();
#if !WINDOWS
        TrySubscribeAccessTokenChanged();
#endif
    }

    private void OnWebResourceRequested(object? sender, Microsoft.Maui.Controls.WebViewWebResourceRequestedEventArgs e)
    {
        const string localFileHost = "https://k7-local-files/";
        var url = e.Uri?.ToString();
        if (url is null || !url.StartsWith(localFileHost, StringComparison.OrdinalIgnoreCase))
            return;

        var relativePath = Uri.UnescapeDataString(url[localFileHost.Length..]);
        var filePath = Path.GetFullPath(Path.Combine(DownloadsBasePath, relativePath));

        if (!filePath.StartsWith(DownloadsBasePathPrefix, StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
            return;

        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var mimeType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".mp4" => "video/mp4",
            ".m4a" or ".aac" => "audio/mp4",
            ".mp3" => "audio/mpeg",
            ".flac" => "audio/flac",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream"
        };

        var stream = File.OpenRead(filePath);
        e.SetResponse(200, mimeType, (IReadOnlyDictionary<string, string>?)null, stream);
        e.Handled = true;
    }

    private void InitializeSplashOverlay()
    {
        var startTime = System.Diagnostics.Stopwatch.GetTimestamp();

        _ = Task.Run(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            try
            {
                await AppReadySignal.WaitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("K7 MAUI - Splash timeout, hiding anyway");
            }

            // Ensure minimum display time so the animation is visible
            var elapsed = System.Diagnostics.Stopwatch.GetElapsedTime(startTime);
            var remaining = TimeSpan.FromMilliseconds(1500) - elapsed;
            if (remaining > TimeSpan.Zero)
                await Task.Delay(remaining);

            MainThread.BeginInvokeOnMainThread(() =>
            {
#if ANDROID
                AndroidStartupLottieOverlay.Dismiss();
#endif
                SplashOverlay.IsVisible = false;
                RootGrid.Children.Remove(SplashOverlay);
            });
        });
    }

    private void OnSplashAnimationLoaded(object? sender, EventArgs e)
    {
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(400), RevealSplashLottie);
    }

    private void RevealSplashLottie()
    {
        if (SplashLogo is null || !SplashLogo.IsVisible)
            return;

        SplashLogo.IsVisible = false;
    }

    protected override bool OnBackButtonPressed()
    {
        HandleBackButton();
        return true;
    }

    internal void HandleBackButton()
    {
        NativeVideoDebug.Log(
            "HandleBackButton playerVisible=" + _playerService.IsVisible
            + " nativeChrome=" + MauiNativeVideoChrome.IsEnabled);

        if (_backButtonService.HandleBackButton())
            return;

        // When native video player is active, prefer native overlay or WebView JS.
        if (_playerService.IsVisible)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (TryHandleNativeVideoBack())
                {
                    NativeVideoDebug.Log("HandleBackButton consumed by native overlay");
                    return;
                }
#if ANDROID
                if (!MauiNativeVideoChrome.IsEnabled
                    && TryEvaluateWebViewJs(
                        "(function(){try{"
                        + "var r=window.K7&&K7.handleVideoTvBack?K7.handleVideoTvBack():'';"
                        + "if(r==='close'){"
                        + "try{if(window.K7TvVideo&&K7TvVideo.closePlayer)K7TvVideo.closePlayer();}"
                        + "catch(e1){}"
                        + "}"
                        + "}catch(e){"
                        + "try{if(window.SpatialNav&&SpatialNav.handleBack)SpatialNav.handleBack();}"
                        + "catch(e2){}"
                        + "}})();"))
                    return;
#endif
                if (_playerService is MAUI.Services.PlayerService ps)
                    ps.OnBackPressed();
            });
            return;
        }

        DispatchBackAsEscape();
    }

#if ANDROID
    /// <summary>Called from K7TvVideo.seek JavascriptInterface (bypasses Blazor circuit).</summary>
    internal void SeekFromTvJs(double seconds)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RememberSeekTarget(seconds);
            SeekNativeVideoAsync(seconds).FireAndForget();
        });
    }

    /// <summary>Called from K7TvVideo.seekBy for short-press relative seeks.</summary>
    internal void SeekByFromTvJs(double deltaSeconds)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var target = Math.Max(0, GetSeekAnchorSeconds() + deltaSeconds);
            if (_playerService.Duration > 0)
                target = Math.Min(target, _playerService.Duration);
            RememberSeekTarget(target);
            SeekNativeVideoAsync(target).FireAndForget();
        });
    }

    /// <summary>Called from K7TvVideo.skip using SkipBack/SkipForward preferences.</summary>
    internal void SkipFromTvJs(int direction)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var back = Math.Max(1, _playerService.SkipBackSeconds);
            var forward = Math.Max(1, _playerService.SkipForwardSeconds);
            var delta = direction < 0 ? -back : forward;
            var anchor = GetSeekAnchorSeconds();
            var target = Math.Max(0, anchor + delta);
            if (_playerService.Duration > 0)
                target = Math.Min(target, _playerService.Duration);
            RememberSeekTarget(target);
            SeekNativeVideoAsync(target).FireAndForget();
        });
    }

    /// <summary>Called from K7TvVideo.closePlayer when Blazor overlay close is wedged.</summary>
    internal void ClosePlayerFromTvJs()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                _playerService.Stop();
                _ = _playerService.HideAsync();
            }
            catch (Exception)
            {
            }
        });
    }
#endif

    private double? _chainedSeekTargetSeconds;
    private DateTime _chainedSeekUtc;
#if !WINDOWS
    private PropertyChangedEventHandler? _pendingSeekStateHandler;
#endif

    /// <summary>
    /// Prefer last seek target when skips arrive faster than CurrentTime updates.
    /// </summary>
    private double GetSeekAnchorSeconds()
    {
        if (_chainedSeekTargetSeconds is double chained
            && (DateTime.UtcNow - _chainedSeekUtc).TotalMilliseconds < 900)
            return chained;

#if !WINDOWS
        try
        {
            var native = NativePlayer.Position.TotalSeconds;
            if (native > 0)
                return native;
        }
        catch
        {
        }
#endif
        return Math.Max(0, _playerService.CurrentTime);
    }

    private void RememberSeekTarget(double seconds)
    {
        _chainedSeekTargetSeconds = seconds;
        _chainedSeekUtc = DateTime.UtcNow;
    }

    internal void DispatchBackAsEscape()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
#if ANDROID
            // Bypass Blazor circuit - after seek/scrub the dispatcher can stall and leave a black shell.
            if (TryEvaluateWebViewJs(
                    "try{if(window.SpatialNav&&SpatialNav.handleBack)SpatialNav.handleBack();}catch(e){}"))
                return;
#endif
            _ = blazorWebView.TryDispatchAsync(async sp =>
            {
                try
                {
                    var js = sp.GetRequiredService<IJSRuntime>();
                    await js.InvokeVoidAsync("SpatialNav.handleBack");
                }
                catch (JSException)
                {
                }
                catch (InvalidOperationException)
                {
                }
                catch (JSDisconnectedException)
                {
                }
            });
        });
    }

    internal void NotifyTvRemoteSelect(string phase, int keyCode, long heldMs)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (MauiNativeVideoChrome.IsEnabled && _playerService.IsVisible)
            {
                NativeVideoDebug.Log("NotifyTvRemoteSelect phase=" + phase + " key=" + keyCode + " heldMs=" + heldMs);
                if (phase is "up" or "long-up")
                    TryHandleNativeVideoKey("select");
                return;
            }

#if ANDROID
            // Direct WebView JS - TryDispatchAsync silently dies after scrub/seek storms.
            if (TryEvaluateWebViewJs(
                    "try{if(window.K7&&K7.onTvRemoteSelect)K7.onTvRemoteSelect("
                    + System.Text.Json.JsonSerializer.Serialize(phase)
                    + ","
                    + keyCode
                    + ","
                    + heldMs
                    + ");}catch(e){}"))
            {
                return;
            }

#endif
            _ = blazorWebView.TryDispatchAsync(async sp =>
            {
                try
                {
                    var js = sp.GetRequiredService<IJSRuntime>();
                    await js.InvokeVoidAsync("K7.onTvRemoteSelect", phase, keyCode, heldMs);
                }
                catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException)
                {
                }
            });
        });
    }

    internal void HandleMediaPlayPause()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (MauiNativeVideoChrome.IsEnabled && _playerService.IsVisible && _nativeOverlay is not null)
            {
                // Route through overlay so next-episode / chrome focus owns the key.
                _ = TryHandleNativeVideoKey("mediaplaypause");
                return;
            }

            if (_playerService.IsVisible)
            {
                if (_playerService.PlaybackState != Server.Domain.Enums.PlaybackState.Playing)
                    _playerService.Play();
                else
                    _playerService.Pause();
            }
            else if (_audioPlayerService.IsVisible)
            {
                if (_audioPlayerService.PlaybackState != Server.Domain.Enums.PlaybackState.Playing)
                    _audioPlayerService.Play();
                else
                    _audioPlayerService.Pause();
            }
        });
    }

    internal void HandleMediaStop()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_playerService.IsVisible)
            {
                _playerService.Stop();
                _playerService.HideAsync();
            }
            else if (_audioPlayerService.IsVisible || _audioPlayerService.IsFullScreenVisible)
            {
                if (_audioPlayerService.IsFullScreenVisible)
                    _audioPlayerService.ToggleFullScreen();
                _audioPlayerService.Stop();
                _audioPlayerService.HideAsync();
            }
        });
    }

    private void InitializePlayer()
    {
        NativePlayer.Volume = _playerService.Volume;
        NativePlayer.ShouldMute = _playerService.IsMuted;
        NativePlayer.MediaOpened += NativePlayer_MediaOpened;
        NativePlayer.MediaEnded += NativePlayer_MediaEnded;
        NativePlayer.MediaFailed += NativePlayer_MediaFailed;
        NativePlayer.PositionChanged += NativePlayer_PositionChanged;
        NativePlayer.PropertyChanged += NativePlayer_PropertyChanged;

        _playerService.SourceChanged += OnSourceChanged;
        _playerService.IsVisibleChanged += OnIsVisibleChanged;
#if !WINDOWS
        _playerService.PlayRequested += HandleVideoPlayRequested;
        _playerService.PauseRequested += HandleVideoPauseRequested;
        _playerService.MuteRequested += HandleVideoMuteRequested;
        _playerService.UnmuteRequest += HandleVideoUnmuteRequested;
        _playerService.VolumeChangeRequested += HandleVideoVolumeChangeRequested;
        _playerService.PlaybackRateChangeRequested += HandleVideoPlaybackRateChangeRequested;
        _playerService.StopRequested += HandleVideoStopRequested;
        _playerService.SeekRequested += HandleVideoSeekRequested;
        _playerService.AspectRatioModeChangeRequested += OnAspectRatioModeChanged;
#endif
        InitializePlayerPlatform();
    }

#if !WINDOWS
    private Task HandleVideoPlayRequested()
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            // Stopped/Ended after a backward seek (especially to zero) may ignore Play()
            // until the timeline position is re-established.
            if (NativePlayer.CurrentState is MediaElementState.Stopped)
            {
                var position = NativePlayer.Position;
                if (position < TimeSpan.Zero)
                    position = TimeSpan.Zero;

                await NativePlayer.SeekTo(position);
            }

            NativePlayer.Play();
        });
    }

    private Task HandleVideoPauseRequested()
    {
        MainThread.BeginInvokeOnMainThread(NativePlayer.Pause);
        return Task.CompletedTask;
    }

    private Task HandleVideoMuteRequested()
    {
        MainThread.BeginInvokeOnMainThread(() => NativePlayer.ShouldMute = true);
        return Task.CompletedTask;
    }

    private Task HandleVideoUnmuteRequested()
    {
        MainThread.BeginInvokeOnMainThread(() => NativePlayer.ShouldMute = false);
        return Task.CompletedTask;
    }

    private Task HandleVideoVolumeChangeRequested(double volume)
    {
        MainThread.BeginInvokeOnMainThread(() => NativePlayer.Volume = volume);
        return Task.CompletedTask;
    }

    private Task HandleVideoPlaybackRateChangeRequested(double rate)
    {
        MainThread.BeginInvokeOnMainThread(() => NativePlayer.Speed = rate);
        return Task.CompletedTask;
    }

    private Task HandleVideoStopRequested()
    {
        MainThread.BeginInvokeOnMainThread(NativePlayer.Stop);
        return Task.CompletedTask;
    }

    private Task HandleVideoSeekRequested(double position) =>
        SeekNativeVideoAsync(position);

    private Task SeekNativeVideoAsync(double positionSeconds)
    {
#if ANDROID
        return SeekAndroidVideoAsync(positionSeconds);
#else
        return SeekMediaElementAsync(
            NativePlayer,
            TimeSpan.FromSeconds(positionSeconds),
            () => _playerService.PlaybackState,
            t => _playerService.CurrentTime = t,
            () => OnAfterNativeVideoSeek());
#endif
    }
#endif

    private void NativePlayer_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
#if WINDOWS
        return;
#else
        if (e.PropertyName == nameof(MediaElement.Duration))
        {
            var duration = NativePlayer.Duration.TotalSeconds;
            if (duration > 0 && duration != _playerService.Duration)
                _playerService.Duration = duration;
        }

        if (e.PropertyName == nameof(MediaElement.ShouldMute))
            _playerService.IsMuted = NativePlayer.ShouldMute;

        if (e.PropertyName == nameof(MediaElement.CurrentState))
        {
            var mediaState = NativePlayer.CurrentState;
            // Opening must not map to Idle: ExoPlayer stays Opening/Buffering while HLS
            // init + early segments load (PGS burn-in can take tens of seconds). Idle made
            // the startup watchdog treat successful segment streaming as "not ready".
            var mapped = mediaState switch
            {
                MediaElementState.Buffering => Server.Domain.Enums.PlaybackState.Buffering,
                MediaElementState.Playing => Server.Domain.Enums.PlaybackState.Playing,
                MediaElementState.Paused => Server.Domain.Enums.PlaybackState.Paused,
                // ExoPlayer stays Opening while HLS init + early segments load.
                MediaElementState.Opening => Server.Domain.Enums.PlaybackState.Buffering,
                MediaElementState.Stopped => NativeVideoPlaybackEnd.ShouldTreatStoppedAsEnded(
                    _openingNativeSource,
                    _playerService.IsVisible,
                    _playerService.PlaybackState,
                    NativePlayer.Duration.TotalSeconds,
                    NativePlayer.Position.TotalSeconds)
                    ? Server.Domain.Enums.PlaybackState.Ended
                    : Server.Domain.Enums.PlaybackState.Idle,
                _ => Server.Domain.Enums.PlaybackState.Unknown,
            };

            _playerService.PlaybackState = mapped;
#if ANDROID
            NativeVideoDebug.Log(
                "MediaState mapped=" + mapped + " native=" + mediaState
                + " pos=" + NativePlayer.Position.TotalSeconds.ToString("F2")
                + "s dur=" + NativePlayer.Duration.TotalSeconds.ToString("F2")
                + "s visible=" + _playerService.IsVisible
                + " pending=" + (_playerService.Source?.PendingSeekTime?.ToString("F1") ?? "null"));

            if (_playerService.IsVisible && MauiNativeVideoChrome.IsEnabled)
            {
                if (mediaState is MediaElementState.Playing)
                {
                    // Keep the veil through resume PendingSeek (first Playing is often at 0:00).
                    var pending = _playerService.Source?.PendingSeekTime;
                    var pos = NativePlayer.Position.TotalSeconds;
                    if (pending is double resumeAt && resumeAt > 1 && pos < resumeAt - 2)
                        _nativeOverlay?.SetLoadingVeil(true);
                    else
                        _nativeOverlay?.NotifyFirstFrameReady();
                }
                else if (mediaState is MediaElementState.Opening
                    || (mediaState is MediaElementState.Buffering
                        && NativePlayer.Position <= TimeSpan.FromSeconds(1))
                    || (mediaState is MediaElementState.Paused
                        && NativePlayer.Position <= TimeSpan.FromSeconds(0.25)))
                {
                    // Startup only - never cover mid-play buffer stalls with an opaque veil.
                    _nativeOverlay?.SetLoadingVeil(true);
                }
            }
            else if (_playerService.IsVisible && mediaState is MediaElementState.Playing)
            {
                ApplyAndroidWebViewShell(seeThroughForVideo: true);
                _ = TryEvaluateWebViewJs(
                    "try{if(window.K7&&K7.setNativePlayerPlaying)K7.setNativePlayerPlaying(true);}catch(e){}");
            }
#endif
        }
#endif
    }

    private void OnSourceChanged(PlayerSource source)
    {
#if WINDOWS
        SyncWindowsStreamAuthContext();

        // All Windows video uses Video.js in WebView2, not native MediaElement.
        return;
#else
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!string.IsNullOrEmpty(source.Url))
            {
                OpenNativePlayerSource(source);
            }
            // PlayIndexedFileAsync sets an empty PlayerSource before ShowAsync + real URL.
            // Do not Stop/Source=null here - that races the subsequent open and can fire
            // MediaFailed after init/seg0 (playback dead, UI stuck at 0:00/0:00).
        });
#endif
    }

#if !WINDOWS
    private void OpenNativePlayerSource(PlayerSource source)
    {
        // ShowAsync and SourceChanged both marshal to the main thread; if visibility is still
        // pending, force the MediaElement visible before Play or ExoPlayer may not bind a surface.
        if (_playerService.IsVisible && !NativePlayer.IsVisible)
            NativePlayer.IsVisible = true;

        _nativeAuthRecoveryCount = 0;

        _openingNativeSource = true;
        try
        {
            // Baseline open path: Stop() then assign Source. Never Source=null first -
            // nulling the surface fires MediaFailed on Android and kills the next open mid-HLS.
            NativePlayer.Stop();
            NativePlayer.ShouldAutoPlay = true;
            // CommunityToolkit.Maui.MediaElement 9.0+ (PR #3169) applies UriMediaSource.HttpHeaders via
            // DefaultHttpDataSource.Factory.SetDefaultRequestProperties for every HLS request.
            // Do not rebind ExoPlayer after MediaOpened - that fights the toolkit and is unnecessary.
            NativePlayer.Source = CreateMediaSourceWithAuth(source.Url!);
            NativeVideoDebug.Log(
                "OpenNativePlayerSource local=" + LocalPlaybackUrl.IsLocalFile(source.Url)
                + " url=" + (LocalPlaybackUrl.IsLocalFile(source.Url) ? "file" : "http"));
            // Apply sync-point seek params before Play so #EXT-X-START / PendingSeek do not exact-seek.
            ConfigureNativeVideoPlayerAfterOpen();
#if ANDROID
            // Android uses system volume; clear any stuck MediaElement mute from earlier volume swipes
            // (native chrome hides the mute button, so users cannot recover otherwise).
            if (_playerService.IsMuted || NativePlayer.ShouldMute)
                _playerService.Unmute();

            if (source.PendingSeekTime is double pendingSeek && pendingSeek > 1)
                RememberSeekTarget(pendingSeek);

            // Toolkit DefaultHttpDataSource uses 8s connect/read timeouts. Server can hold init.m4s
            // up to ~90s while ffmpeg seeks (mid-stream resume). Rebind with longer timeouts.
            // Skip local/offline files: HTTP factory cannot open filesystem or file:// URLs.
            if (!LocalPlaybackUrl.IsLocalFile(source.Url))
                BindAndroidExoPlayerWithLongHttpTimeouts(source.Url!);
#endif
            NativePlayer.Play();
        }
        finally
        {
            _openingNativeSource = false;
        }

        AttachPendingSeekHandler(source);
        if (MauiNativeVideoChrome.IsEnabled)
            _nativeOverlay?.SetLoadingVeil(true);
    }

    private void AttachPendingSeekHandler(PlayerSource source)
    {
        if (source.PendingSeekTime is not double seekTime)
            return;

        NativeVideoDebug.Log("AttachPendingSeek seekTime=" + seekTime.ToString("F1") + "s");

        if (_pendingSeekStateHandler is not null)
        {
            NativePlayer.PropertyChanged -= _pendingSeekStateHandler;
            _pendingSeekStateHandler = null;
        }

        void TryApplyPendingSeek(string reason)
        {
            if (!ReferenceEquals(_playerService.Source, source))
                return;
            if (source.PendingSeekTime is not double pending)
                return;

            var duration = NativePlayer.Duration.TotalSeconds;
            var position = NativePlayer.Position.TotalSeconds;
            var playing = NativePlayer.CurrentState is MediaElementState.Playing;

            // startSeconds in the manifest often lands us near the target already.
            if (duration > 0 && position > 0 && Math.Abs(position - pending) <= 4)
            {
                source.PendingSeekTime = null;
                if (_pendingSeekStateHandler is not null)
                {
                    NativePlayer.PropertyChanged -= _pendingSeekStateHandler;
                    _pendingSeekStateHandler = null;
                }

                NativeVideoDebug.Log(
                    "PendingSeek skip near target=" + pending.ToString("F1")
                    + "s pos=" + position.ToString("F1") + "s reason=" + reason);
                return;
            }

            // Wait until Playing with a real duration (Playing+dur=0 is a transient ExoPlayer state).
            if (!playing || duration <= 0)
                return;

            if (_pendingSeekStateHandler is not null)
            {
                NativePlayer.PropertyChanged -= _pendingSeekStateHandler;
                _pendingSeekStateHandler = null;
            }

            source.PendingSeekTime = null;
            NativeVideoDebug.Log(
                "PendingSeek apply target=" + pending.ToString("F1")
                + "s state=" + NativePlayer.CurrentState + " reason=" + reason);
            SeekNativeVideoAsync(pending).FireAndForget();
        }

        void OnStateChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(MediaElement.CurrentState) or nameof(MediaElement.Duration)
                or nameof(MediaElement.Position))
                TryApplyPendingSeek(e.PropertyName ?? "prop");
        }

        _pendingSeekStateHandler = OnStateChanged;
        NativePlayer.PropertyChanged += OnStateChanged;
        // In case we attached after Playing+duration already arrived.
        TryApplyPendingSeek("attach");

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(8000);
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    if (!_playerService.IsVisible)
                        return;
                    if (!ReferenceEquals(_playerService.Source, source))
                        return;
                    if (source.PendingSeekTime is not double stillPending)
                        return;

                    var position = NativePlayer.Position.TotalSeconds;
                    if (position > 0 && Math.Abs(position - stillPending) <= 4)
                    {
                        source.PendingSeekTime = null;
                        if (_pendingSeekStateHandler is not null)
                        {
                            NativePlayer.PropertyChanged -= _pendingSeekStateHandler;
                            _pendingSeekStateHandler = null;
                        }

                        NativeVideoDebug.Log(
                            "PendingSeek failsafe skip near target=" + stillPending.ToString("F1")
                            + "s pos=" + position.ToString("F1") + "s");
                        return;
                    }

                    NativeVideoDebug.Log(
                        "PendingSeek failsafe target=" + stillPending.ToString("F1")
                        + "s state=" + NativePlayer.CurrentState);
                    if (_pendingSeekStateHandler is not null)
                    {
                        NativePlayer.PropertyChanged -= _pendingSeekStateHandler;
                        _pendingSeekStateHandler = null;
                    }

                    source.PendingSeekTime = null;
                    SeekNativeVideoAsync(stillPending).FireAndForget();
                    NativePlayer.Play();
                });
            }
            catch
            {
            }
        });
    }
#endif

    private void OnIsVisibleChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
#if WINDOWS
            ConfigureWindowsVideoPlayerLayout();
            OnNativeVideoVisibilityChanged(_playerService.IsVisible);
#else
            NativePlayer.IsVisible = _playerService.IsVisible;
            OnNativeVideoVisibilityChanged(_playerService.IsVisible);

            if (_playerService.IsVisible)
            {
                // Black under MediaElement + opaque black WebView shell while HLS loads.
                // Do not set the WebView Transparent yet - on Android TV that clears white
                // over the still-empty TextureView (overlay-on-movie -> white -> black -> film).
                BackgroundColor = Colors.Black;
                Padding = new Thickness(0);
#if ANDROID || IOS
                DeviceDisplay.Current.KeepScreenOn = true;
                Microsoft.Maui.Devices.DeviceDisplay.Current.MainDisplayInfoChanged += OnDisplayInfoChanged;
                SetLandscapeOrientation();
#endif
#if ANDROID
                if (!MauiNativeVideoChrome.IsEnabled)
                {
                    _ = TryEvaluateWebViewJs(
                        "try{if(window.K7&&K7.setNativePlayerActive)K7.setNativePlayerActive(true,false);"
                        + "if(window.K7&&K7.setNativePlayerPlaying)K7.setNativePlayerPlaying(false);}catch(e){}");
                    ApplyAndroidWebViewShell(seeThroughForVideo: false);
                }
                // Native chrome: keep focus off the hidden WebView. Legacy HUD: bounce to WebView.
                SetVideoFocusOwnership(active: true);
#else
                if (!MauiNativeVideoChrome.IsEnabled)
                    blazorWebView.BackgroundColor = Colors.Transparent;
#endif
            }
            else
            {
                BackgroundColor = Colors.Transparent;
                blazorWebView.BackgroundColor = Colors.Transparent;
                NativePlayer.Stop();
                NativePlayer.Source = null;
#if ANDROID || IOS
                DeviceDisplay.Current.KeepScreenOn = false;
                Microsoft.Maui.Devices.DeviceDisplay.Current.MainDisplayInfoChanged -= OnDisplayInfoChanged;
                RestoreOrientation();
#endif
#if ANDROID
                // Video closed: brand shell, drop focus bounce, clear native-player-active.
                ApplyAndroidWebViewShellBrand();
                SetVideoFocusOwnership(active: false);
                ClearNativePlayerActiveShell();
#endif
            }
#endif
        });
    }

#if !WINDOWS
    private void OnAspectRatioModeChanged(AspectRatioMode mode)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            NativePlayer.Aspect = mode switch
            {
                AspectRatioMode.Fill => Aspect.AspectFill,
                AspectRatioMode.Stretch => Aspect.Fill,
                _ => Aspect.AspectFit,
            };
        });
    }
#endif

    private void NativePlayer_MediaOpened(object? sender, EventArgs e)
    {
#if !WINDOWS
        // Toolkit raises MediaOpened right after ExoPlayer.Prepare(), before HLS has finished
        // loading - Duration=0 here is normal and does not mean the master playlist succeeded.
        var duration = NativePlayer.Duration.TotalSeconds;
        if (duration > 0 && duration != _playerService.Duration)
            _playerService.Duration = duration;

        // Do not force Idle here - PropertyChanged owns state (Opening/Buffering/Playing).
        ConfigureNativeVideoPlayerAfterOpen();
#endif
    }

    private void NativePlayer_MediaEnded(object? sender, EventArgs e)
    {
#if !WINDOWS
        if (_openingNativeSource || !_playerService.IsVisible)
            return;

        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Ended;
#endif
    }

    private void NativePlayer_MediaFailed(object? sender, MediaFailedEventArgs e)
    {
        // Native MediaElement path: never Abort/Stop here - MediaFailed fires spuriously on
        // Source swaps and thrash-killed working Android streams. Still report once to the server.
        var detail = e.ErrorMessage ?? "(null)";
#if ANDROID
        detail += FormatAndroidPlayerErrorDetail();
#endif
        var stateDetail =
            " CurrentState="
            + NativePlayer.CurrentState
            + " Position="
            + NativePlayer.Position.TotalSeconds.ToString("F2")
            + "s Duration="
            + NativePlayer.Duration.TotalSeconds.ToString("F2")
            + "s";

#if ANDROID
        // Toolkit may race and re-apply the 8s HttpDataSource after our first bind.
        if (_androidHttpTimeoutRetryCount < 2
            && (detail.Contains("ERROR_CODE_IO_NETWORK_CONNECTION_TIMEOUT", StringComparison.Ordinal)
                || detail.Contains("SocketTimeoutException", StringComparison.Ordinal)))
        {
            var url = _playerService.Source?.Url;
            if (!string.IsNullOrEmpty(url) && _playerService.IsVisible)
            {
                _androidHttpTimeoutRetryCount++;
                var resumeAt = CaptureNativeVideoResumePosition();
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    if (!_playerService.IsVisible || string.IsNullOrEmpty(_playerService.Source?.Url))
                        return;
                    RebindAndroidNativeVideoPreservingPosition(_playerService.Source.Url, resumeAt);
                });
            }
        }
        else if (ShouldAttemptNativeAuthRecovery(detail))
        {
            _ = TryRecoverNativeVideoAuthAsync(detail);
        }
#endif

#if !WINDOWS
        ReportNativePlayerMediaFailedToServer(detail + stateDetail);
#endif
    }

#if !WINDOWS
    private void ReportNativePlayerMediaFailedToServer(string failureDetail)
    {
        try
        {
            var source = _playerService.Source;
            var sessionId = source?.StreamSessionId?.ToString() ?? "(none)";
            var indexedFileId = source?.IndexedFileId?.ToString() ?? "(none)";
            var quality = _playerService.SelectedQuality?.Label ?? "(none)";
            var redactedUrl = RedactUrl(source?.Url);

            var dedupeKey = failureDetail + "|" + sessionId + "|" + quality;
            var now = DateTime.UtcNow;
            if (_lastMediaFailedReportKey == dedupeKey
                && now - _lastMediaFailedReportUtc < MediaFailedReportDedupeWindow)
            {
                return;
            }

            _lastMediaFailedReportKey = dedupeKey;
            _lastMediaFailedReportUtc = now;

            var platform =
#if ANDROID
                "Android";
#elif IOS
                "iOS";
#elif MACCATALYST
                "MacCatalyst";
#else
                "Unknown";
#endif

            var message =
                "ErrorMessage="
                + failureDetail
                + " url="
                + redactedUrl
                + " StreamSessionId="
                + sessionId
                + " IndexedFileId="
                + indexedFileId
                + " quality="
                + quality
                + " Platform="
                + platform
                + " UsesWebVideoPlayer="
                + WindowsVideoPlayback.UsesWebVideoPlayer;

            var services = Application.Current?.Handler?.MauiContext?.Services
                ?? IPlatformApplication.Current?.Services;
            var reporter = services?.GetService<IClientErrorReporter>();
            reporter?.ReportError(
                new InvalidOperationException(message),
                "NativePlayer.MediaFailed",
                notifyUser: false);
        }
        catch
        {
            // Best-effort - never throw from MediaFailed.
        }
    }

    /// <summary>
    /// Redacts sensitive query values while keeping parameter names for diagnostics.
    /// Example: ?access_token=abc&amp;Quality=720p -> ?access_token=REDACTED&amp;Quality=720p
    /// </summary>
    private static string RedactUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return "(null)";

        var queryIndex = url.IndexOf('?');
        if (queryIndex < 0)
            return url;

        var path = url[..queryIndex];
        var query = url[(queryIndex + 1)..];
        if (string.IsNullOrEmpty(query))
            return url;

        var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = part[..eq];
            if (IsSensitiveQueryKey(key))
                parts[i] = key + "=REDACTED";
        }

        return path + "?" + string.Join('&', parts);
    }

    private static bool IsSensitiveQueryKey(string key)
    {
        if (SensitiveQueryKeys.Contains(key))
            return true;

        // Catch variants like DefaultAccessToken / streamToken without listing every alias.
        return key.Contains("token", StringComparison.OrdinalIgnoreCase)
            || key.Contains("auth", StringComparison.OrdinalIgnoreCase);
    }
#endif

    private void NativePlayer_PositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
#if WINDOWS
        return;
#else
        _playerService.CurrentTime = e.Position.TotalSeconds;
#endif
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        NativePlayer.Stop();
#if !WINDOWS
        NativeAudioPlayer.Stop();
#endif
        DetachEventHandlers();
    }

    private void OnNativePlayerCloseClicked(object? sender, EventArgs e) => OnNativePlayerCloseClickedAsync().FireAndForget();

    private async Task OnNativePlayerCloseClickedAsync()
    {
        _playerService.Stop();
        await _playerService.HideAsync();
    }

    private void InitializeAudioPlayer()
    {
        _audioPlayerService.PlayerUxSettingsChanged += HandleAudioPlayerUxSettingsChanged;
        _audioPlayerService.PlaybackStateChanged += HandleAudioPlaybackKeepScreenChanged;
        ApplyKeepScreenOnFromAudio();

#if ANDROID || IOS || WINDOWS
        // Android/iOS: native services handle audio.
        // Windows: WebView2 audioplayer.js (same path as browser) handles audio.
        return;
#else
        WireNativeAudioElement(NativeAudioPlayer);
        WireNativeAudioElement(NativeAudioCrossfadePlayer);
        ActiveAudioPlayer.Volume = _audioPlayerService.Volume;
        ActiveAudioPlayer.ShouldMute = _audioPlayerService.IsMuted;

        _audioPlayerService.CurrentTrackChanged += OnAudioCurrentTrackChanged;
        _audioPlayerService.SourceChanged += OnAudioSourceChanged;
        _audioPlayerService.PlayRequested += HandleAudioPlayRequested;
        _audioPlayerService.PauseRequested += HandleAudioPauseRequested;
        _audioPlayerService.StopRequested += HandleAudioStopRequested;
        _audioPlayerService.SeekRequested += HandleAudioSeekRequested;
        _audioPlayerService.MuteRequested += HandleAudioMuteRequested;
        _audioPlayerService.UnmuteRequested += HandleAudioUnmuteRequested;
        _audioPlayerService.VolumeChangeRequested += HandleAudioVolumeChangeRequested;
        _audioPlayerService.FadeOutRequested += HandleAudioFadeOutRequested;
        _audioPlayerService.FadeResetRequested += HandleAudioFadeResetRequested;
        _audioPlayerService.CrossfadeRequested += HandleAudioCrossfadeRequested;
        _audioPlayerService.GaplessPrebufferRequested += HandleAudioGaplessPrebufferRequested;
        _audioPlayerService.LoudnessSettingsChanged += HandleAudioLoudnessSettingsChanged;
        RefreshAudioLoudnessGain();
#endif
    }

#if !ANDROID && !IOS && !WINDOWS
    private void WireNativeAudioElement(MediaElement player)
    {
        player.PositionChanged += AudioPlayer_PositionChanged;
        player.MediaEnded += AudioPlayer_MediaEnded;
        player.MediaFailed += AudioPlayer_MediaFailed;
        player.PropertyChanged += NativeAudioPlayer_PropertyChanged;
    }

    private void UnwireNativeAudioElement(MediaElement player)
    {
        player.PositionChanged -= AudioPlayer_PositionChanged;
        player.MediaEnded -= AudioPlayer_MediaEnded;
        player.MediaFailed -= AudioPlayer_MediaFailed;
        player.PropertyChanged -= NativeAudioPlayer_PropertyChanged;
    }
#endif

    private void HandleAudioPlayerUxSettingsChanged() => ApplyKeepScreenOnFromAudio();

    private void HandleAudioPlaybackKeepScreenChanged(Server.Domain.Enums.PlaybackState _) => ApplyKeepScreenOnFromAudio();

    private void ApplyKeepScreenOnFromAudio()
    {
        // Video path may also set KeepScreenOn; OR the music preference while playing.
        var musicWantsScreen = _audioPlayerService.KeepScreenOn
            && _audioPlayerService.PlaybackState is Server.Domain.Enums.PlaybackState.Playing
                or Server.Domain.Enums.PlaybackState.Buffering;
        if (musicWantsScreen)
            DeviceDisplay.Current.KeepScreenOn = true;
        else if (!_playerService.IsVisible)
            DeviceDisplay.Current.KeepScreenOn = false;
    }

#if !ANDROID && !IOS && !WINDOWS
    private double AudioPeakVolume => _audioPlayerService.Volume * _audioLoudnessLinearGain;

    private void HandleAudioLoudnessSettingsChanged() => RefreshAudioLoudnessGain(applyToPlayer: true);

    private void RefreshAudioLoudnessGain(bool applyToPlayer = false)
    {
        var track = _audioPlayerService.CurrentTrack;
        var linear = LoudnessGainHelper.ComputeLinearGain(
            _audioPlayerService.LoudnessEnabled,
            _audioPlayerService.LoudnessTargetLufs,
            _audioPlayerService.LoudnessPreampDb,
            track?.LoudnessLufs,
            track?.ReplayGainTrackGain);
        _audioLoudnessLinearGain = LoudnessGainHelper.ApplySoftLimiter(linear, _audioPlayerService.LimiterEnabled);

        if (applyToPlayer && !_audioCrossfadeInProgress)
            MainThread.BeginInvokeOnMainThread(() => ActiveAudioPlayer.Volume = AudioPeakVolume);
    }

    private Task HandleAudioGaplessPrebufferRequested(PlayerSource source)
    {
        if (string.IsNullOrEmpty(source.Url)) return Task.CompletedTask;

        _audioGaplessPrebufferedUrl = source.Url;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IdleAudioPlayer.Volume = 0;
            IdleAudioPlayer.Source = CreateMediaSourceWithAuth(source.Url);
        });
        return Task.CompletedTask;
    }

    private Task HandleAudioPlayRequested()
    {
        MainThread.BeginInvokeOnMainThread(ActiveAudioPlayer.Play);
        return Task.CompletedTask;
    }

    private Task HandleAudioPauseRequested()
    {
        MainThread.BeginInvokeOnMainThread(ActiveAudioPlayer.Pause);
        return Task.CompletedTask;
    }

    private Task HandleAudioStopRequested()
    {
        MainThread.BeginInvokeOnMainThread(ActiveAudioPlayer.Stop);
        return Task.CompletedTask;
    }

    private Task HandleAudioSeekRequested(double position) =>
        SeekMediaElementAsync(
            ActiveAudioPlayer,
            TimeSpan.FromSeconds(position),
            () => _audioPlayerService.PlaybackState,
            t => _audioPlayerService.CurrentTime = t);

    private Task HandleAudioMuteRequested()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            NativeAudioPlayer.ShouldMute = true;
            NativeAudioCrossfadePlayer.ShouldMute = true;
        });
        return Task.CompletedTask;
    }

    private Task HandleAudioUnmuteRequested()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            NativeAudioPlayer.ShouldMute = false;
            NativeAudioCrossfadePlayer.ShouldMute = false;
        });
        return Task.CompletedTask;
    }

    private Task HandleAudioVolumeChangeRequested(double volume)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_audioCrossfadeInProgress)
                ActiveAudioPlayer.Volume = volume * _audioLoudnessLinearGain;
        });
        return Task.CompletedTask;
    }

    private async Task HandleAudioCrossfadeRequested(PlayerSource source, double durationSeconds)
    {
        if (string.IsNullOrEmpty(source.Url)) return;

        _audioCrossfadeInProgress = true;
        _audioFadeCts?.Cancel();
        _audioFadeCts?.Dispose();
        _audioFadeCts = new CancellationTokenSource();
        var ct = _audioFadeCts.Token;
        var peak = AudioPeakVolume;
        var outgoing = ActiveAudioPlayer;
        var incoming = IdleAudioPlayer;
        var promoted = false;

        // Service already switched CurrentTrack; drive UI from the incoming player (not outgoing leftover time).
        _audioPlayerService.CurrentTime = 0;
        if (_audioPlayerService.CurrentTrack?.Duration is { } trackDuration and > 0)
            _audioPlayerService.Duration = trackDuration;

        try
        {
            var alreadyPrepared = string.Equals(_audioGaplessPrebufferedUrl, source.Url, StringComparison.Ordinal);
            if (!alreadyPrepared)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    incoming.Volume = 0;
                    incoming.Source = CreateMediaSourceWithAuth(source.Url);
                });
                await WaitMediaElementReadyAsync(incoming, ct);
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                incoming.Volume = 0;
                incoming.Play();
            });

            await EqualPowerNativeCrossfadeAsync(outgoing, incoming, Math.Max(0.25, durationSeconds), peak, ct);

            // Promote incoming in place (Web-style swap). Do not reload Source or we restart from 0.
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _audioRolesSwapped = !_audioRolesSwapped;
                ActiveAudioPlayer.Volume = peak;
                IdleAudioPlayer.Stop();
                IdleAudioPlayer.Source = null;
                IdleAudioPlayer.Volume = 0;
                _audioPlayerService.CurrentTime = ActiveAudioPlayer.Position.TotalSeconds;
                var duration = ActiveAudioPlayer.Duration.TotalSeconds;
                if (duration > 0)
                    _audioPlayerService.Duration = duration;
            });

            promoted = true;
            _audioGaplessPrebufferedUrl = null;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[K7-Audio] Crossfade failed: {ex.Message}");
        }
        finally
        {
            if (!promoted)
            {
                // Restore audible output on the still-active player after a cancelled/failed fade.
                try
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        ActiveAudioPlayer.Volume = peak;
                        IdleAudioPlayer.Volume = 0;
                        try { IdleAudioPlayer.Stop(); } catch { /* best effort */ }
                        try { IdleAudioPlayer.Source = null; } catch { /* best effort */ }
                    });
                }
                catch
                {
                    // ignore
                }

                _audioGaplessPrebufferedUrl = null;
            }

            _audioCrossfadeInProgress = false;
            _audioPlayerService.NotifyCrossfadeCompleted();
        }
    }

    private static async Task WaitMediaElementReadyAsync(MediaElement player, CancellationToken ct)
    {
        for (var i = 0; i < 100; i++)
        {
            ct.ThrowIfCancellationRequested();
            if (player.CurrentState is MediaElementState.Playing or MediaElementState.Paused or MediaElementState.Buffering)
                return;
            await Task.Delay(50, ct);
        }
    }

    private async Task EqualPowerNativeCrossfadeAsync(
        MediaElement outgoing,
        MediaElement incoming,
        double durationSeconds,
        double peakVolume,
        CancellationToken ct)
    {
        var steps = Math.Max(1, (int)Math.Ceiling(durationSeconds * 20));
        var stepMs = Math.Max(1, (int)(durationSeconds * 1000 / steps));

        for (var i = 1; i <= steps; i++)
        {
            ct.ThrowIfCancellationRequested();
            var ratio = i / (double)steps;
            var fadeOut = Math.Cos(ratio * Math.PI / 2.0) * peakVolume;
            var fadeIn = Math.Sin(ratio * Math.PI / 2.0) * peakVolume;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                outgoing.Volume = fadeOut;
                incoming.Volume = fadeIn;
            });
            await Task.Delay(stepMs, ct);
        }
    }

    private async Task HandleAudioFadeOutRequested(double durationSeconds)
    {
        _audioFadeCts?.Cancel();
        _audioFadeCts?.Dispose();
        _audioFadeCts = new CancellationTokenSource();
        var ct = _audioFadeCts.Token;
        var startVolume = ActiveAudioPlayer.Volume;

        try
        {
            await FadeNativeAudioVolumeAsync(startVolume, 0, Math.Max(0.25, durationSeconds), ct);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task FadeNativeAudioVolumeAsync(double from, double to, double durationSeconds, CancellationToken ct)
    {
        var steps = Math.Max(1, (int)Math.Ceiling(durationSeconds * 20));
        var stepMs = Math.Max(1, (int)(durationSeconds * 1000 / steps));

        for (var i = 1; i <= steps; i++)
        {
            ct.ThrowIfCancellationRequested();
            var t = i / (double)steps;
            var volume = from + ((to - from) * t);
            await MainThread.InvokeOnMainThreadAsync(() => ActiveAudioPlayer.Volume = volume);
            await Task.Delay(stepMs, ct);
        }
    }

    private Task HandleAudioFadeResetRequested()
    {
        _audioFadeCts?.Cancel();
        MainThread.BeginInvokeOnMainThread(() => ActiveAudioPlayer.Volume = AudioPeakVolume);
        return Task.CompletedTask;
    }

    private void NativeAudioPlayer_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is not MediaElement element)
            return;

        var trackTimeFrom = _audioCrossfadeInProgress ? IdleAudioPlayer : ActiveAudioPlayer;
        if (!ReferenceEquals(element, trackTimeFrom))
            return;

        if (e.PropertyName == nameof(MediaElement.Duration))
        {
            var duration = element.Duration.TotalSeconds;
            if (duration > 0 && duration != _audioPlayerService.Duration)
                _audioPlayerService.Duration = duration;
        }

        if (e.PropertyName == nameof(MediaElement.CurrentState))
        {
            // During crossfade the outgoing player may go Idle/Stopped; keep UI on Playing.
            if (_audioCrossfadeInProgress && ReferenceEquals(element, ActiveAudioPlayer)
                && element.CurrentState is MediaElementState.Stopped or MediaElementState.Paused)
                return;

            _audioPlayerService.PlaybackState = element.CurrentState switch
            {
                MediaElementState.Buffering => Server.Domain.Enums.PlaybackState.Buffering,
                MediaElementState.Playing => Server.Domain.Enums.PlaybackState.Playing,
                MediaElementState.Paused => Server.Domain.Enums.PlaybackState.Paused,
                MediaElementState.Opening => Server.Domain.Enums.PlaybackState.Idle,
                MediaElementState.Stopped => Server.Domain.Enums.PlaybackState.Idle,
                _ => Server.Domain.Enums.PlaybackState.Unknown,
            };
        }
    }
#endif

    private void DetachEventHandlers()
    {
        if (_eventsDetached)
            return;

        _eventsDetached = true;

        blazorWebView.WebResourceRequested -= OnWebResourceRequested;

        NativePlayer.MediaOpened -= NativePlayer_MediaOpened;
        NativePlayer.MediaEnded -= NativePlayer_MediaEnded;
        NativePlayer.MediaFailed -= NativePlayer_MediaFailed;
        NativePlayer.PositionChanged -= NativePlayer_PositionChanged;
        NativePlayer.PropertyChanged -= NativePlayer_PropertyChanged;

        _playerService.SourceChanged -= OnSourceChanged;
        _playerService.IsVisibleChanged -= OnIsVisibleChanged;
        _nativeOverlay?.Detach();
        _audioPlayerService.PlayerUxSettingsChanged -= HandleAudioPlayerUxSettingsChanged;
        _audioPlayerService.PlaybackStateChanged -= HandleAudioPlaybackKeepScreenChanged;
#if !WINDOWS
        if (_accessTokenChangedSubscribed && _authStateProvider is not null)
        {
            _authStateProvider.AccessTokenChanged -= OnAccessTokenChanged;
            _accessTokenChangedSubscribed = false;
        }

        _playerService.PlayRequested -= HandleVideoPlayRequested;
        _playerService.PauseRequested -= HandleVideoPauseRequested;
        _playerService.MuteRequested -= HandleVideoMuteRequested;
        _playerService.UnmuteRequest -= HandleVideoUnmuteRequested;
        _playerService.VolumeChangeRequested -= HandleVideoVolumeChangeRequested;
        _playerService.PlaybackRateChangeRequested -= HandleVideoPlaybackRateChangeRequested;
        _playerService.StopRequested -= HandleVideoStopRequested;
        _playerService.SeekRequested -= HandleVideoSeekRequested;
        _playerService.AspectRatioModeChangeRequested -= OnAspectRatioModeChanged;
#endif

#if !ANDROID && !IOS && !WINDOWS
        UnwireNativeAudioElement(NativeAudioPlayer);
        UnwireNativeAudioElement(NativeAudioCrossfadePlayer);

        _audioPlayerService.CurrentTrackChanged -= OnAudioCurrentTrackChanged;
        _audioPlayerService.SourceChanged -= OnAudioSourceChanged;
        _audioPlayerService.PlayRequested -= HandleAudioPlayRequested;
        _audioPlayerService.PauseRequested -= HandleAudioPauseRequested;
        _audioPlayerService.StopRequested -= HandleAudioStopRequested;
        _audioPlayerService.SeekRequested -= HandleAudioSeekRequested;
        _audioPlayerService.MuteRequested -= HandleAudioMuteRequested;
        _audioPlayerService.UnmuteRequested -= HandleAudioUnmuteRequested;
        _audioPlayerService.VolumeChangeRequested -= HandleAudioVolumeChangeRequested;
        _audioPlayerService.FadeOutRequested -= HandleAudioFadeOutRequested;
        _audioPlayerService.FadeResetRequested -= HandleAudioFadeResetRequested;
        _audioPlayerService.CrossfadeRequested -= HandleAudioCrossfadeRequested;
        _audioPlayerService.GaplessPrebufferRequested -= HandleAudioGaplessPrebufferRequested;
        _audioPlayerService.LoudnessSettingsChanged -= HandleAudioLoudnessSettingsChanged;
        _audioFadeCts?.Cancel();
        _audioFadeCts?.Dispose();
        _audioFadeCts = null;
#endif

        DetachPlayerPlatform();
    }

    partial void DetachPlayerPlatform();

#if !ANDROID && !IOS && !WINDOWS
    private void OnAudioCurrentTrackChanged(AudioQueueItem? track)
    {
        RefreshAudioLoudnessGain(applyToPlayer: !_audioCrossfadeInProgress);
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (track is null) return;

            ActiveAudioPlayer.MetadataTitle = track.Title;
            ActiveAudioPlayer.MetadataArtist = track.Artist ?? "";
            ActiveAudioPlayer.MetadataArtworkUrl = _k7ServerService.GetAbsoluteUri(track.CoverUrl)?.AbsoluteUri ?? "";
        });
    }

    private void OnAudioSourceChanged(PlayerSource source)
    {
        if (_audioCrossfadeInProgress) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (string.IsNullOrEmpty(source.Url)) return;

            if (string.Equals(_audioGaplessPrebufferedUrl, source.Url, StringComparison.Ordinal)
                && IdleAudioPlayer.Source is not null)
            {
                var peak = AudioPeakVolume;
                IdleAudioPlayer.Volume = peak;
                IdleAudioPlayer.Play();
                ActiveAudioPlayer.Volume = 0;
                _ = PromoteWindowsGaplessAsync(peak);
                return;
            }

            ActiveAudioPlayer.Source = CreateMediaSourceWithAuth(source.Url);
            ActiveAudioPlayer.Volume = AudioPeakVolume;
            ActiveAudioPlayer.Play();
            _audioGaplessPrebufferedUrl = null;
        });
    }

    private async Task PromoteWindowsGaplessAsync(double peak)
    {
        try
        {
            // Incoming already playing on Idle - promote via role swap (no reload from 0).
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _audioRolesSwapped = !_audioRolesSwapped;
                ActiveAudioPlayer.Volume = peak;
                IdleAudioPlayer.Stop();
                IdleAudioPlayer.Source = null;
                IdleAudioPlayer.Volume = 0;
                _audioGaplessPrebufferedUrl = null;
                _audioPlayerService.CurrentTime = ActiveAudioPlayer.Position.TotalSeconds;
            });
        }
        catch
        {
            // Best-effort gapless handoff.
        }
    }

    private void AudioPlayer_PositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
        if (sender is not MediaElement element)
            return;

        // During crossfade, UI follows the incoming track (Idle before promote).
        var source = _audioCrossfadeInProgress ? IdleAudioPlayer : ActiveAudioPlayer;
        if (!ReferenceEquals(element, source))
            return;

        _audioPlayerService.CurrentTime = e.Position.TotalSeconds;
    }

    private void AudioPlayer_MediaEnded(object? sender, EventArgs e)
    {
        if (sender is not MediaElement element)
            return;
        if (_audioCrossfadeInProgress)
            return;
        if (!ReferenceEquals(element, ActiveAudioPlayer))
            return;

        _audioPlayerService.OnTrackEndedAsync().FireAndForget();
    }

    private void AudioPlayer_MediaFailed(object? sender, MediaFailedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[K7-Audio] Playback failed: {e.ErrorMessage}");

        if (_audioSuppressMediaFailed)
            return;

        // Idle/crossfade failures should not tear down the audible player.
        if (sender is MediaElement failed
            && !ReferenceEquals(failed, ActiveAudioPlayer)
            && !_audioCrossfadeInProgress)
        {
            _audioGaplessPrebufferedUrl = null;
            return;
        }

        // Do not auto-reload here: clearing Source and calling LoadAndPlay again
        // re-enters MediaFailed (Source=null / SourceNotSupported) and dances the UI forever.
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _audioSuppressMediaFailed = true;
            try
            {
                _audioFadeCts?.Cancel();
                _audioCrossfadeInProgress = false;
                _audioGaplessPrebufferedUrl = null;
                _audioRolesSwapped = false;
                _audioPlayerService.NotifyCrossfadeCompleted();

                try { ActiveAudioPlayer.Stop(); } catch { /* best effort */ }
                try { IdleAudioPlayer.Stop(); } catch { /* best effort */ }
                ActiveAudioPlayer.Volume = AudioPeakVolume;
                IdleAudioPlayer.Volume = 0;

                _audioPlayerService.PlaybackState = Server.Domain.Enums.PlaybackState.Idle;
            }
            finally
            {
                _audioSuppressMediaFailed = false;
            }
        });
    }
#endif

    private MediaSource CreateMediaSourceWithAuth(string url)
    {
        if (LocalPlaybackUrl.TryGetLocalFilesystemPath(url, out var localPath) && File.Exists(localPath))
        {
            // FromUri(file://) without HTTP headers: ExoPlayer DefaultDataSource opens the file.
            // FromFile(raw path) can SetUri("/data/...") which some ExoPlayer binds mishandle;
            // never attach Authorization headers or the toolkit switches to DefaultHttpDataSource.
            return MediaSource.FromUri(LocalPlaybackUrl.CreateFileUri(localPath));
        }

        var authValue = ResolveNativePlayerAuthorizationHeader();
        if (!string.IsNullOrEmpty(authValue))
        {
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = authValue
            };
            return MediaSource.FromUri(new Uri(url), headers);
        }

        return MediaSource.FromUri(url);
    }

#if !WINDOWS
    private void TrySubscribeAccessTokenChanged()
    {
        if (_accessTokenChangedSubscribed)
            return;

        var services = Application.Current?.Handler?.MauiContext?.Services
            ?? IPlatformApplication.Current?.Services;
        _authStateProvider = services?.GetService<ICustomAuthenticationStateProvider>();
        if (_authStateProvider is null)
            return;

        _authStateProvider.AccessTokenChanged += OnAccessTokenChanged;
        _accessTokenChangedSubscribed = true;
    }

    private void OnAccessTokenChanged(object? sender, EventArgs e)
    {
        if (!_playerService.IsVisible || string.IsNullOrEmpty(_playerService.Source?.Url))
            return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!_playerService.IsVisible || string.IsNullOrEmpty(_playerService.Source?.Url))
                return;

#if ANDROID
            // Proactive AuthSessionKeeper refresh (~2 min while near expiry) used to full-rebind
            // ExoPlayer + seek-to-current, causing a multi-second buffer stall. Update the shared
            // HTTP Authorization header so the next segment fetch uses the new Bearer instead.
            ApplyExoPlayerHttpAuthHeaders();
            NativeVideoDebug.Log("AccessTokenChanged applied Exo HTTP auth without rebind");
#else
            var resumeAt = Math.Max(CaptureNativeVideoResumePosition(), _authRebindResumeOverride ?? 0);
            ReopenNativePlayerSourcePreservingPosition(_playerService.Source, resumeAt);
#endif
        });
    }

    private static bool ShouldAttemptNativeAuthRecovery(string detail) =>
        detail.Contains("ResponseCode=401", StringComparison.Ordinal)
        || detail.Contains("ERROR_CODE_IO_BAD_HTTP_STATUS", StringComparison.Ordinal);

    private async Task TryRecoverNativeVideoAuthAsync(string detail)
    {
        if (!_playerService.IsVisible || string.IsNullOrEmpty(_playerService.Source?.Url))
            return;

        var now = DateTime.UtcNow;
        if (_nativeAuthRecoveryCount >= 2 && now - _lastNativeAuthRecoveryUtc < TimeSpan.FromMinutes(2))
            return;

        if (now - _lastNativeAuthRecoveryUtc < TimeSpan.FromSeconds(5))
            return;

        _nativeAuthRecoveryCount++;
        _lastNativeAuthRecoveryUtc = now;

        var resumeAt = CaptureNativeVideoResumePosition();
        _authRebindResumeOverride = resumeAt;
        var rejectedToken = ResolveNativePlayerBearerToken();

        try
        {
            TrySubscribeAccessTokenChanged();
            var auth = _authStateProvider
                ?? Application.Current?.Handler?.MauiContext?.Services?.GetService<ICustomAuthenticationStateProvider>()
                ?? IPlatformApplication.Current?.Services?.GetService<ICustomAuthenticationStateProvider>();

            if (auth is not null)
                await auth.TryRefreshAsync(rejectedAccessToken: rejectedToken, forceRefresh: true);

            // Always rebind: AccessTokenChanged may no-op if the token string did not change,
            // and ExoPlayer must pick up storage/HttpClient Bearer after a 401.
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!_playerService.IsVisible || string.IsNullOrEmpty(_playerService.Source?.Url))
                    return;

#if ANDROID
                RebindAndroidNativeVideoPreservingPosition(_playerService.Source.Url, resumeAt);
#else
                ReopenNativePlayerSourcePreservingPosition(_playerService.Source, resumeAt);
#endif
            });
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!_playerService.IsVisible || string.IsNullOrEmpty(_playerService.Source?.Url))
                    return;

#if ANDROID
                RebindAndroidNativeVideoPreservingPosition(_playerService.Source.Url, resumeAt);
#else
                ReopenNativePlayerSourcePreservingPosition(_playerService.Source, resumeAt);
#endif
            });
        }
        finally
        {
            _authRebindResumeOverride = null;
        }
    }

    private double CaptureNativeVideoResumePosition()
    {
        var fromPlayer = NativePlayer.Position.TotalSeconds;
        var fromService = _playerService.CurrentTime;
        var pending = _playerService.Source?.PendingSeekTime ?? 0;
        return Math.Max(Math.Max(fromPlayer, fromService), pending);
    }

    private void ReopenNativePlayerSourcePreservingPosition(PlayerSource source, double resumeAt)
    {
        if (string.IsNullOrEmpty(source.Url))
            return;

        if (resumeAt > 1)
            source.PendingSeekTime = resumeAt;

        _openingNativeSource = true;
        try
        {
            NativePlayer.Stop();
            NativePlayer.ShouldAutoPlay = true;
            NativePlayer.Source = CreateMediaSourceWithAuth(source.Url);
            ConfigureNativeVideoPlayerAfterOpen();
            NativePlayer.Play();
        }
        finally
        {
            _openingNativeSource = false;
        }

        AttachPendingSeekHandler(source);
    }
#endif

    private string? ResolveNativePlayerAuthorizationHeader()
    {
        var token = ResolveNativePlayerBearerToken();
        return string.IsNullOrEmpty(token) ? null : "Bearer " + token;
    }

    private string? ResolveNativePlayerBearerToken()
    {
        var services = Application.Current?.Handler?.MauiContext?.Services
            ?? IPlatformApplication.Current?.Services;
        var deviceStorage = services?.GetService<IDeviceStorageService>();
        var fromStorage = deviceStorage?.Get(K7.Shared.PreferenceKeys.ACCESS_TOKEN);
        if (!string.IsNullOrEmpty(fromStorage))
            return fromStorage;

        return _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization?.Parameter;
    }

#if ANDROID || IOS
    private static void SetLandscapeOrientation()
    {
        DeviceDisplay.Current.KeepScreenOn = true;
#if ANDROID
        SetLandscapeOrientationPlatform();
#endif
    }

    private static void RestoreOrientation()
    {
        DeviceDisplay.Current.KeepScreenOn = false;
#if ANDROID
        RestoreOrientationPlatform();
#endif
    }

    private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
    {
    }
#endif

    partial void InitializePlayerPlatform();

    partial void ConfigureNativeVideoPlayerAfterOpen();

    partial void OnAfterNativeVideoSeek();

#if WINDOWS
    partial void ConfigureWindowsVideoPlayerLayout();

    private void SyncWindowsStreamAuthContext() =>
        Platforms.Windows.WindowsStreamAuthContext.UpdateFrom(_k7ServerService);
#endif

    private static Task SeekMediaElementAsync(
        MediaElement mediaElement,
        TimeSpan position,
        Func<Server.Domain.Enums.PlaybackState>? getPlaybackState = null,
        Action<double>? setCurrentTime = null,
        Action? afterSeek = null) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var target = position < TimeSpan.Zero ? TimeSpan.Zero : position;
            var resumePlayback = getPlaybackState?.Invoke()
                is Server.Domain.Enums.PlaybackState.Playing
                or Server.Domain.Enums.PlaybackState.Buffering;

            try
            {
                await mediaElement.SeekTo(target);

                // Some native stacks ignore an exact-zero seek after jumping forward.
                if (target == TimeSpan.Zero
                    && mediaElement.Duration > TimeSpan.Zero
                    && mediaElement.Position.TotalSeconds > 1)
                {
                    await mediaElement.SeekTo(TimeSpan.FromMilliseconds(1));
                    await mediaElement.SeekTo(TimeSpan.Zero);
                }
            }
            catch (Exception)
            {
            }

            setCurrentTime?.Invoke(target.TotalSeconds);
            afterSeek?.Invoke();

            if (resumePlayback
                && mediaElement.CurrentState is not MediaElementState.Playing
                && mediaElement.CurrentState is not MediaElementState.Buffering)
            {
                mediaElement.Play();
            }
        });
}
