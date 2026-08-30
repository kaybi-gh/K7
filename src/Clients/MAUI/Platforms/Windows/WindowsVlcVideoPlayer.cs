#if WINDOWS
using System.Globalization;
using K7.Clients.MAUI.Playback;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using LibVLCSharp;
using LibVLCSharp.MAUI;
using DeviceType = K7.Server.Domain.Enums.DeviceType;
using MediaPlayer = LibVLCSharp.MediaPlayer;
using WinVideoView = LibVLCSharp.Platforms.Windows.VideoView;

namespace K7.Clients.MAUI.Platforms.Windows;

/// <summary>
/// LibVLC 4 video surface for Windows Direct Play and local files.
/// Windows HLS transcode uses Video.js in WebView2 (not this player).
/// </summary>
internal sealed class WindowsVlcVideoPlayer : IDisposable
{
    private readonly Grid _host;
    private readonly VideoView _videoView;
    private LibVLC? _libVlc;
    private MediaPlayer? _player;
    private Media? _media;
    private WindowsHlsAudioSidecar? _hlsAudio;
    private VlcAuthProxy? _authProxy;
    private WinVideoView? _platformView;
    private WindowsVlcD3d11Output? _d3d11;
    private string[]? _swapChainOptions;
    private string? _lastNativeError;
    private string? _currentUrl;
    private string? _pendingUrl;
    private string? _pendingAuthorization;
    private double _pendingStartSeconds;
    private bool _active;
    private bool _suppressEnded;
    private bool _firstFrameRaised;
    private bool _handlerHooked;
    private bool _swapChainReady;
    private AspectRatioMode _aspect = AspectRatioMode.Fit;
    private int? _pendingAudioOrdinal;
    private int? _pendingHlsAudioTrackIndex;
    private int? _pendingSubtitleOrdinal;
    private bool _pendingIsHls;
    private bool _engineIsHls;
    private bool _firstFrameNotified;
    private bool _pendingPreciseStart;
    private bool _overlayOwnsTextSubs;
    private bool _holdTransport;
    private bool _holdWallArmed;
    private DateTime _holdStartedUtc;
    private double _pinnedPosition;
    private double _pinnedDuration;
    private double _volume01 = 1;
    private bool _muted;
    private double _rate = 1;
    private int _audioBindAttempts;
    private bool _hlsResumeApplied;
    private bool _hlsResumeSeekIssued;
    private int _hlsResumeAttempts;
    private int _clockTickId;
    private double _lastPublishedSeconds = -1;
    private long _ticksPerSecond = VlcTime.MicrosecondsPerSecond;
    private bool _ticksScaleLogged;

    public WindowsVlcVideoPlayer(Grid host)
    {
        _host = host;
        _videoView = new VideoView
        {
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Fill,
            InputTransparent = true,
            BackgroundColor = Colors.Black,
            ZIndex = 3,
            IsVisible = false
        };
        host.Children.Add(_videoView);
        _videoView.HandlerChanged += OnVideoViewHandlerChanged;
        _videoView.SizeChanged += OnVideoViewSizeChanged;
        _handlerHooked = true;
    }

    public bool IsActive => _active;

    public double PositionSeconds => ReadVlcSeconds();

    public double DurationSeconds =>
        _pinnedDuration > 1
            ? _pinnedDuration
            : ReadVlcDurationSeconds();

    public event Action? Playing;
    public event Action? Paused;
    public event Action? Ended;
    public event Action<string>? EncounteredError;
    public event Action<double>? PositionChanged;
    public event Action<double>? DurationChanged;
    public event Action? FirstFrame;
    public event Action? Reopening;

    public void Play(
        string url,
        string? authorizationHeader,
        double startSeconds,
        int? audioOrdinal = null,
        int? subtitleOrdinal = null,
        int? hlsAudioTrackIndex = null,
        double knownDuration = 0)
    {
        var nextIsHls = StreamingSourceKind.IsHls(mimeType: null, url);
        if (_libVlc is not null && nextIsHls != _engineIsHls)
        {
            VlcPlayerLog.Info("vlc recreate engine for " + (nextIsHls ? "hls" : "direct"));
            _suppressEnded = true;
            _clockTickId++;
            DetachD3d11();
            StopPlayback(keepSession: true);
            DisposeLibVlcCore();
            TearDownHlsAudioEngine();
            _suppressEnded = false;
        }

        _pendingUrl = url;
        _currentUrl = url;
        _pendingAuthorization = authorizationHeader;
        _pendingStartSeconds = startSeconds;
        _pendingAudioOrdinal = audioOrdinal;
        _pendingSubtitleOrdinal = subtitleOrdinal;
        _pendingHlsAudioTrackIndex = hlsAudioTrackIndex;
        _pendingIsHls = nextIsHls;
        _engineIsHls = nextIsHls;
        _firstFrameNotified = false;
        _firstFrameRaised = false;
        // HLS also holds the overlay clock until demux/EXT-X-START lands.
        _holdTransport = startSeconds > 1;
        _holdWallArmed = false;
        if (_holdTransport)
            _holdStartedUtc = DateTime.UtcNow;
        _pinnedPosition = startSeconds > 0 ? startSeconds : 0;
        _pinnedDuration = knownDuration > 1 ? knownDuration : 0;
        _lastPublishedSeconds = _pinnedPosition;
        _ticksPerSecond = VlcTime.MicrosecondsPerSecond;
        _ticksScaleLogged = false;
        _hlsResumeApplied = !_pendingIsHls || startSeconds <= 1;
        _hlsResumeSeekIssued = false;
        _hlsResumeAttempts = 0;
        _audioBindAttempts = 0;
        _clockTickId++;
        _suppressEnded = false;
        _active = true;
        _videoView.IsVisible = true;
        HookPlatformVideoView();
        if (!_swapChainReady)
            VlcPlayerLog.Info("vlc wait swapchain");
        TryStartWhenSwapChainReady();
    }

    public void UpdateAuthorization(string? authorizationHeader)
    {
        _pendingAuthorization = authorizationHeader;
        _authProxy?.SetAuthorization(authorizationHeader);
    }

    public void Resume()
    {
        if (_player is null || !_active)
            return;

        _player.SetPause(false);
        if (!_player.IsPlaying)
            _player.Play();
        _hlsAudio?.Resume();
        StartClockTick();
    }

    public void Pause()
    {
        if (_player is null || !_active)
            return;

        _clockTickId++;
        _player.SetPause(true);
        _hlsAudio?.Pause();
        var seconds = ReadVlcSeconds();
        if (seconds >= 0)
        {
            _lastPublishedSeconds = seconds;
            PositionChanged?.Invoke(seconds);
        }
    }

    /// <summary>
    /// Hide the decode surface without tearing down LibVLC (e.g. remote-control UI in WebView).
    /// </summary>
    public void SetSurfaceVisible(bool visible)
    {
        _videoView.IsVisible = visible && _active;
    }

    public void Stop()
    {
        // Invalidate callbacks before any native teardown so queued Posts no-op.
        _clockTickId++;
        _suppressEnded = true;
        _active = false;
        _pendingUrl = null;
        _videoView.IsVisible = false;

        // Detach D3D before MediaPlayer.Stop: Present during Stop/Dispose races into
        // ExecutionEngineException / AccessViolation on fast Direct -> HLS swaps.
        DetachD3d11();
        StopPlayback();
        DisposeLibVlcCore();
        _swapChainReady = false;
        _swapChainOptions = null;
    }

    /// <summary>
    /// App exit path: clear D3D callbacks immediately, soft-release WinUI-owned D3D wrappers,
    /// then Stop/Dispose LibVLC off the UI thread. Disposing the SwapChain on Closing races
    /// Present and throws ExecutionEngineException.
    /// </summary>
    public void PrepareForAppExit()
    {
        _clockTickId++;
        _suppressEnded = true;
        _active = false;
        _pendingUrl = null;
        try
        {
            _videoView.IsVisible = false;
        }
        catch
        {
        }

        var d3d = _d3d11;
        var player = _player;
        var libVlc = _libVlc;
        var media = _media;
        var proxy = _authProxy;
        var hls = _hlsAudio;
        _d3d11 = null;
        _player = null;
        _libVlc = null;
        _media = null;
        _authProxy = null;
        _hlsAudio = null;
        _swapChainReady = false;
        _swapChainOptions = null;

        if (d3d is not null)
        {
            try
            {
                d3d.FirstPresented -= OnD3d11FirstPresented;
            }
            catch
            {
            }

            // Clear native Present callbacks while the MediaPlayer is still alive.
            try
            {
                d3d.Detach(player);
            }
            catch
            {
            }

            try
            {
                d3d.SoftReleaseForAppExit();
            }
            catch
            {
            }
        }

        if (player is not null)
        {
            try
            {
                Unhook(player);
            }
            catch
            {
            }

            try
            {
                _videoView.MediaPlayer = null;
            }
            catch
            {
            }
        }

        if (_handlerHooked)
        {
            _videoView.HandlerChanged -= OnVideoViewHandlerChanged;
            _videoView.SizeChanged -= OnVideoViewSizeChanged;
            _handlerHooked = false;
        }

        UnhookPlatformVideoView();

        _ = Task.Run(() =>
        {
            try
            {
                player?.Stop();
            }
            catch
            {
            }

            try
            {
                player?.Dispose();
            }
            catch
            {
            }

            try
            {
                media?.Dispose();
            }
            catch
            {
            }

            try
            {
                libVlc?.Dispose();
            }
            catch
            {
            }

            try
            {
                proxy?.Dispose();
            }
            catch
            {
            }

            try
            {
                hls?.Dispose();
            }
            catch
            {
            }

            // Keep native callback targets alive until Stop finished (cleared callbacks).
            GC.KeepAlive(d3d);
        });
    }

    public void Seek(double seconds)
    {
        if (_player is null || !_active)
            return;

        var target = Math.Max(0, seconds);
        _pendingStartSeconds = target;
        _hlsResumeApplied = true;
        _hlsResumeSeekIssued = true;

        // Direct Play over HTTP: SetTime is often ignored (seek-to-0 stays mid-film).
        // Audio track switches already reopen with :start-time; do the same for seek.
        if (!_pendingIsHls)
        {
            SeekDirectReopen(target);
            return;
        }

        _holdTransport = target > 1;
        _holdWallArmed = false;
        if (_holdTransport)
            _holdStartedUtc = DateTime.UtcNow;
        _pinnedPosition = target;
        _player.SetTime(VlcTime.FromSeconds(target, _ticksPerSecond), fast: false);
        var duration = ReadVlcDurationSeconds();
        if (duration > 1)
            _player.SetPosition((float)Math.Clamp(target / duration, 0, 1), fast: false);
        if (_hlsAudio is not null)
        {
            var audioUrl = _authProxy?.HlsAudioSlaveUrl;
            if (!string.IsNullOrEmpty(audioUrl))
                _hlsAudio.SeekTo(audioUrl, target);
        }
        PositionChanged?.Invoke(target);
    }

    private void SeekDirectReopen(double targetSeconds)
    {
        var url = _currentUrl;
        if (string.IsNullOrEmpty(url) || !_active)
            return;

        // Prefer the pinned/metadata duration. VLC Length is often 0 while stopped
        // for reopen, which previously wiped the seekbar total.
        var fromPlayer = DurationSeconds;
        if (fromPlayer > 1)
            _pinnedDuration = Math.Max(_pinnedDuration, fromPlayer);

        _pendingStartSeconds = targetSeconds;
        _pinnedPosition = targetSeconds;
        _holdTransport = true;
        _holdWallArmed = false;
        _holdStartedUtc = DateTime.UtcNow;
        _pendingPreciseStart = true;
        _pendingUrl = url;
        _firstFrameRaised = false;
        _firstFrameNotified = false;
        _audioBindAttempts = 0;
        _lastPublishedSeconds = targetSeconds;
        _suppressEnded = true;
        Reopening?.Invoke();
        VlcPlayerLog.Info(
            "vlc reopen reason=seek start="
            + targetSeconds.ToString("F3", CultureInfo.InvariantCulture)
            + "s duration="
            + _pinnedDuration.ToString("F1", CultureInfo.InvariantCulture));
        if (_pinnedDuration > 1)
            DurationChanged?.Invoke(_pinnedDuration);
        PositionChanged?.Invoke(targetSeconds);
        StopPlayback(keepSession: true);
        _suppressEnded = false;
        StartMedia(url);
    }

    /// <summary>Keep metadata duration across Direct Play reopen/seek.</summary>
    public void PinDuration(double seconds)
    {
        if (seconds <= 1)
            return;

        _pinnedDuration = Math.Max(_pinnedDuration, seconds);
        DurationChanged?.Invoke(_pinnedDuration);
    }

    public void SetVolume(double volume01)
    {
        _volume01 = Math.Clamp(volume01, 0, 1);
        ApplyOutputLevel();
    }

    public void SetMuted(bool muted)
    {
        _muted = muted;
        ApplyOutputLevel();
    }

    public void SetRate(double rate)
    {
        _rate = Math.Clamp(rate, 0.25, 4.0);
        if (_player is null)
            return;

        _player.SetRate((float)_rate);
        _hlsAudio?.SetRate(_rate);
    }

    public void ApplyAspect(AspectRatioMode mode)
    {
        _aspect = mode;
        ApplyAspectCore();
    }

    public bool TrySelectAudio(int ordinal, string? language, string? name)
    {
        if (_player is null)
            return false;

        var tracks = VlcTracks.Snapshot(_player, TrackType.Audio);
        try
        {
            VlcTracks.Log("audio", tracks, ordinal, language, name, VlcTracks.SelectedId(_player, TrackType.Audio));
            if (tracks.Length == 0)
                return false;

            if (!VlcTracks.TryResolve(tracks, ordinal, language, name, out var index, out var track))
                return false;

            _pendingAudioOrdinal = index;
            if (track.Selected)
                return true;

            // HLS audio is the adaptive rendition. Mid-play Select kills mmdevice;
            // language changes rebuild the master URL instead.
            if (_pendingIsHls)
                return VlcTracks.SelectedId(_player, TrackType.Audio) is not null;

            ReopenAtCurrent("audio");
            return true;
        }
        finally
        {
            VlcTracks.DisposeAll(tracks);
        }
    }

    public bool TrySelectSubtitle(int? ordinal, string? language, string? name)
    {
        if (_player is null)
            return false;

        if (ordinal is null)
        {
            _pendingSubtitleOrdinal = null;
            _overlayOwnsTextSubs = false;
            _player.Unselect(TrackType.Text);
            VlcPlayerLog.Info("vlc sub off");
            return true;
        }

        var tracks = VlcTracks.Snapshot(_player, TrackType.Text);
        try
        {
            VlcTracks.Log("sub", tracks, ordinal, language, name, VlcTracks.SelectedId(_player, TrackType.Text));
            if (tracks.Length == 0)
                return false;

            if (!VlcTracks.TryResolve(tracks, ordinal, language, name, out var index, out var track))
                return false;

            _pendingSubtitleOrdinal = index;
            if (track.Selected)
                return true;

            if (_pendingIsHls)
                return false;

            ReopenAtCurrent("sub");
            return true;
        }
        finally
        {
            VlcTracks.DisposeAll(tracks);
        }
    }

    public void SetOverlayOwnsTextSubs(bool owns)
    {
        _overlayOwnsTextSubs = owns;
        if (_player is null || !owns)
            return;

        _pendingSubtitleOrdinal = null;
        _player.Unselect(TrackType.Text);
        VlcPlayerLog.Info("vlc sub overlay");
    }

    public void RefreshSubtitleStyle()
    {
    }

    public void LogEsTracks()
    {
        if (_player is null)
            return;

        var audio = VlcTracks.Snapshot(_player, TrackType.Audio);
        var subs = VlcTracks.Snapshot(_player, TrackType.Text);
        try
        {
            VlcTracks.Log("audio", audio, _pendingAudioOrdinal, null, null, VlcTracks.SelectedId(_player, TrackType.Audio));
            VlcTracks.Log("sub", subs, _pendingSubtitleOrdinal, null, null, VlcTracks.SelectedId(_player, TrackType.Text));
        }
        finally
        {
            VlcTracks.DisposeAll(audio);
            VlcTracks.DisposeAll(subs);
        }
    }

    public void Dispose()
    {
        Stop();
        if (_handlerHooked)
        {
            _videoView.HandlerChanged -= OnVideoViewHandlerChanged;
            _videoView.SizeChanged -= OnVideoViewSizeChanged;
            _handlerHooked = false;
        }

        UnhookPlatformVideoView();
        try
        {
            _host.Children.Remove(_videoView);
        }
        catch
        {
        }
    }

    private void OnVideoViewHandlerChanged(object? sender, EventArgs e)
    {
        HookPlatformVideoView();
        TryStartWhenSwapChainReady();
    }

    private void OnVideoViewSizeChanged(object? sender, EventArgs e)
    {
        HookPlatformVideoView();
        NotifyD3d11Size();
        if (_libVlc is null)
            TryStartWhenSwapChainReady();
    }

    private void HookPlatformVideoView()
    {
        if (_videoView.Handler?.PlatformView is not WinVideoView platform)
            return;

        if (ReferenceEquals(_platformView, platform))
        {
            TryReadExistingSwapChain(platform);
            return;
        }

        UnhookPlatformVideoView();
        _platformView = platform;
        if (_libVlc is not null)
        {
            TearDownEngine();
            if (_active && string.IsNullOrEmpty(_pendingUrl) && !string.IsNullOrEmpty(_currentUrl))
                _pendingUrl = _currentUrl;
        }

        platform.Initialized += OnPlatformVideoViewInitialized;
        TryReadExistingSwapChain(platform);
    }

    private void UnhookPlatformVideoView()
    {
        if (_platformView is null)
            return;

        _platformView.Initialized -= OnPlatformVideoViewInitialized;
        _platformView = null;
    }

    private void OnPlatformVideoViewInitialized(object? sender, LibVLCSharp.Platforms.Windows.InitializedEventArgs e) =>
        Post(() =>
        {
            ApplySwapChainOptions(e.SwapChainOptions);
            TryStartWhenSwapChainReady();
        });

    private void TryReadExistingSwapChain(WinVideoView platform)
    {
        try
        {
            ApplySwapChainOptions(platform.SwapChainOptions);
        }
        catch (InvalidOperationException)
        {
            _swapChainReady = false;
        }
    }

    private void ApplySwapChainOptions(string[] options)
    {
        if (options.Length == 0)
            return;

        var hadEngine = _libVlc is not null;
        var changed = _swapChainOptions is null
            || _swapChainOptions.Length != options.Length
            || !_swapChainOptions.SequenceEqual(options);
        _swapChainOptions = options;
        _swapChainReady = true;
        if (!changed || hadEngine)
            return;

        TearDownEngine();
        if (_active && string.IsNullOrEmpty(_pendingUrl) && !string.IsNullOrEmpty(_currentUrl))
            _pendingUrl = _currentUrl;
    }

    private void TryStartWhenSwapChainReady()
    {
        if (!_active || string.IsNullOrEmpty(_pendingUrl))
            return;

        if (!_swapChainReady)
            return;

        EnsureEngine();
        BindVideoView();
        StartPending();
    }

    private void EnsureEngine()
    {
        if (_libVlc is not null && _player is not null)
            return;

        if (_swapChainOptions is null || _swapChainOptions.Length == 0)
            return;

        var style = VlcSubtitleStyle.ToVlcInstanceOptions(DeviceType.Desktop);
        var libDir = Path.Combine(AppContext.BaseDirectory, "libvlc", "win-x64");
        if (!File.Exists(Path.Combine(libDir, "libvlc.dll")))
        {
            throw new InvalidOperationException(
                "LibVLC 4 natives missing at " + libDir + ". Rebuild so K7CopyLibVlcWindowsNatives runs.");
        }

        Core.Initialize(libDir);
        var args = new List<string>
        {
            "--no-osd",
            "--aout=mmdevice",
            "--mmdevice-passthrough=0",
            "--network-caching=1000"
        };
        args.AddRange(style);
        try
        {
            _libVlc = new LibVLC(enableDebugLogs: true, args.ToArray());
        }
        catch (VLCException)
        {
            VlcPlayerLog.Warn("vlc ctor options rejected, retrying without style");
            try
            {
                _libVlc = new LibVLC(enableDebugLogs: true, "--no-osd", "--aout=mmdevice");
            }
            catch (VLCException)
            {
                VlcPlayerLog.Warn("vlc ctor options rejected, retrying minimal");
                _libVlc = new LibVLC(enableDebugLogs: true);
            }
        }

        _libVlc.SetUserAgent("K7", "K7");
        _libVlc.Log += OnLibVlcLog;
        _player = new MediaPlayer(_libVlc);
        _player.SetAudioOutput("mmdevice");
        _player.SetAudioOutputMixmode(AudioOutputMixmode.Stereo);
        BindD3d11Output();
        Hook(_player);
        BindVideoView();
        VlcPlayerLog.Info(
            "vlc engine windows libvlc4 d3d11-callbacks mmdevice stereo"
            + (_engineIsHls ? " hls" : " direct"));
    }

    private void TearDownEngine()
    {
        DetachD3d11();
        DisposeLibVlcCore();
        TearDownHlsAudioEngine();
    }

    private void DetachD3d11()
    {
        if (_d3d11 is null)
            return;

        try
        {
            _d3d11.FirstPresented -= OnD3d11FirstPresented;
            _d3d11.Detach(_player);
            _d3d11.Dispose();
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc d3d detach " + ex.GetType().Name);
        }

        _d3d11 = null;
    }

    private void DisposeLibVlcCore()
    {
        if (_player is not null)
        {
            Unhook(_player);
            try
            {
                _videoView.MediaPlayer = null;
            }
            catch
            {
            }

            try
            {
                _player.Stop();
            }
            catch
            {
            }

            try
            {
                _player.Dispose();
            }
            catch (Exception ex)
            {
                VlcPlayerLog.Warn("vlc mediaplayer dispose " + ex.GetType().Name);
            }

            _player = null;
        }

        if (_libVlc is not null)
        {
            try
            {
                _libVlc.Log -= OnLibVlcLog;
                _libVlc.Dispose();
            }
            catch (Exception ex)
            {
                VlcPlayerLog.Warn("vlc libvlc dispose " + ex.GetType().Name);
            }

            _libVlc = null;
        }
    }

    private void TearDownHlsAudioEngine()
    {
        _hlsAudio?.Dispose();
        _hlsAudio = null;
    }

    private void BindD3d11Output()
    {
        if (_player is null || _swapChainOptions is null)
            return;

        DetachD3d11();

        _d3d11 = new WindowsVlcD3d11Output();
        _d3d11.FirstPresented += OnD3d11FirstPresented;
        if (!_d3d11.TryAttach(_player, _swapChainOptions))
        {
            DetachD3d11();
            return;
        }

        NotifyD3d11Size();
    }

    private void NotifyD3d11Size()
    {
        if (_d3d11 is null)
            return;

        var width = _videoView.Width;
        var height = _videoView.Height;
        if (double.IsNaN(width) || double.IsNaN(height) || width <= 0 || height <= 0)
            return;

        var density = DeviceDisplay.Current.MainDisplayInfo.Density;
        var scale = density > 0 ? density : 1;
        _d3d11.NotifyPixelSize(
            (uint)Math.Max(1, Math.Round(width * scale)),
            (uint)Math.Max(1, Math.Round(height * scale)));
    }

    private void BindVideoView()
    {
        if (_player is null || _videoView.Handler?.PlatformView is null)
            return;

        if (!ReferenceEquals(_videoView.MediaPlayer, _player))
            _videoView.MediaPlayer = _player;
    }

    private void StartPending()
    {
        var url = _pendingUrl;
        if (string.IsNullOrEmpty(url) || _libVlc is null || _player is null)
            return;

        _pendingUrl = null;
        _suppressEnded = true;
        StopPlayback(keepSession: false);
        _suppressEnded = false;
        _firstFrameRaised = false;
        _firstFrameNotified = false;
        _audioBindAttempts = 0;
        StartMedia(url);
    }

    private void StartMedia(string url)
    {
        if (_libVlc is null || _player is null)
            return;

        Media media;
        var via = "http";
        if (LocalPlaybackUrl.TryGetLocalFilesystemPath(url, out var localPath))
        {
            if (!File.Exists(localPath))
                VlcPlayerLog.Warn("vlc local missing path=" + localPath);

            media = new Media(localPath, FromType.FromPath);
            AddMediaPlaybackOptions(media);
            via = "file";
        }
        else if (_pendingIsHls)
        {
            var playUrl = EnsurePlayProxy(url);
            // Open the video media playlist directly. Windows LibVLC adaptive
            // never follows STREAM-INF (even video-only masters stayed at Length=0).
            if (!string.IsNullOrEmpty(_authProxy?.HlsVideoPlayUrl))
            {
                playUrl = _authProxy.HlsVideoPlayUrl;
                // Media playlist already has #EXT-X-START from startSeconds - do not
                // SetTime (that raced the demux and blocked the audio slave).
                _hlsResumeApplied = true;
                _hlsResumeSeekIssued = true;
            }
            via = _authProxy is { LocalUrl: not null } ? "http-proxy-hls" : "http-query";
            media = new Media(playUrl, FromType.FromLocation);
            // Match Android: longer cache helps adaptive fMP4.
            media.AddOption(":network-caching=2500");
            media.AddOption(":http-reconnect");
            // HLS subtitle renditions (spu/auto) paint on the video and do not
            // clear. Text cues are the XAML sidecar, same as Android.
            media.AddOption(":no-spu");
            // Demuxed audio is WinRT MediaPlayer; keep this player video-only.
            media.AddOption(":no-audio");
            if (_authProxy is null)
                AddHttpExtraHeader(media, _pendingAuthorization);
        }
        else
        {
            var playUrl = EnsurePlayProxy(url);
            via = _authProxy is { LocalUrl: not null } ? "http-proxy" : "http-query";
            media = new Media(playUrl, FromType.FromLocation);
            media.AddOption(":network-caching=3000");
            media.AddOption(":http-reconnect");
            if (_authProxy is null)
                AddHttpExtraHeader(media, _pendingAuthorization);
            AddMediaPlaybackOptions(media);
        }

        _media = media;
        ApplyAspectCore();
        _player.Play(media);
        ApplyOutputLevel();
        // Do not wait for Length: mid-film HLS can report Length late and the
        // MF sidecar never started (no win-audio logs).
        if (_pendingIsHls)
            StartHlsAudioSidecar();
        StartClockTick();
        LogPlay(url, via);
    }

    private void StartHlsAudioSidecar()
    {
        if (!_pendingIsHls || _hlsAudio is { IsActive: true })
            return;

        // LibVLC / WinRT AMS never pull audio-only fMP4 segments. Decode via MF+WASAPI.
        var audioUrl = _authProxy?.HlsAudioSlaveUrl;
        if (string.IsNullOrEmpty(audioUrl))
            return;

        _hlsAudio?.Dispose();
        _hlsAudio = new WindowsHlsAudioSidecar();
        _hlsAudio.SetVolume(_volume01, _muted);
        _hlsAudio.SetRate(_rate);
        _hlsAudio.Play(audioUrl, Math.Max(0, _pendingStartSeconds));
    }

    private void LogPlay(string url, string via)
    {
        var precise = _pendingPreciseStart;
        _pendingPreciseStart = false;
        VlcPlayerLog.Info(
            "vlc play url="
            + VlcPlayerLog.SummarizeUrl(url)
            + " start="
            + _pendingStartSeconds.ToString("F3", CultureInfo.InvariantCulture)
            + "s auth="
            + !string.IsNullOrEmpty(_pendingAuthorization)
            + " via="
            + via
            + " audio="
            + (_pendingAudioOrdinal?.ToString(CultureInfo.InvariantCulture) ?? "-")
            + " sub="
            + (_pendingSubtitleOrdinal?.ToString(CultureInfo.InvariantCulture) ?? "-")
            + " hlsAudio="
            + (_pendingHlsAudioTrackIndex?.ToString(CultureInfo.InvariantCulture) ?? "-")
            + (precise ? " seek=precise" : ""));
    }

    private void AddMediaPlaybackOptions(Media media)
    {
        if (_pendingIsHls)
            return;

        if (_pendingPreciseStart)
            media.AddOption(":no-input-fast-seek");
        else
            media.AddOption(":input-fast-seek");

        if (_pendingStartSeconds > 0)
        {
            media.AddOption(
                ":start-time="
                + _pendingStartSeconds.ToString("F3", CultureInfo.InvariantCulture));
        }

        if (_pendingAudioOrdinal is int audio && audio >= 0)
        {
            media.AddOption(
                ":audio-track=" + audio.ToString(CultureInfo.InvariantCulture));
        }

        if (_pendingSubtitleOrdinal is int sub && sub >= 0)
        {
            media.AddOption(
                ":sub-track=" + sub.ToString(CultureInfo.InvariantCulture));
        }
        else
        {
            media.AddOption(":sub-track=-1");
        }
    }

    private void ReopenAtCurrent(string reason)
    {
        var url = _currentUrl;
        if (string.IsNullOrEmpty(url) || !_active)
            return;

        // After :start-time, MediaPlayer.Time often restarts near 0 (relative).
        // Prefer the overlay clock so audio/seek reopen does not jump to the start.
        var resumeAt = ResolveResumeSeconds();

        if (_pinnedDuration <= 1)
        {
            var duration = ReadVlcDurationSeconds();
            if (duration > 1)
                _pinnedDuration = duration;
        }

        _pendingStartSeconds = resumeAt;
        _pinnedPosition = resumeAt;
        _holdTransport = !_pendingIsHls;
        _holdWallArmed = false;
        if (_holdTransport)
            _holdStartedUtc = DateTime.UtcNow;
        _pendingPreciseStart = !_pendingIsHls;
        _pendingUrl = url;
        TearDownHlsAudioEngine();
        _firstFrameRaised = false;
        _firstFrameNotified = false;
        _audioBindAttempts = 0;
        _hlsResumeApplied = !_pendingIsHls || resumeAt <= 1;
        _hlsResumeSeekIssued = false;
        _hlsResumeAttempts = 0;
        _lastPublishedSeconds = resumeAt;
        _suppressEnded = true;
        Reopening?.Invoke();
        if (_pinnedDuration > 1)
            DurationChanged?.Invoke(_pinnedDuration);
        PositionChanged?.Invoke(resumeAt);
        VlcPlayerLog.Info(
            "vlc reopen reason="
            + reason
            + " start="
            + _pendingStartSeconds.ToString("F3", CultureInfo.InvariantCulture)
            + "s audio="
            + (_pendingAudioOrdinal?.ToString(CultureInfo.InvariantCulture) ?? "-")
            + " sub="
            + (_pendingSubtitleOrdinal?.ToString(CultureInfo.InvariantCulture) ?? "-"));
        StopPlayback(keepSession: true);
        _suppressEnded = false;
        StartMedia(url);
    }

    /// <summary>
    /// Playback position for reopen/seek. Ignores relative VLC Time after :start-time
    /// unless it clearly matches the published overlay clock.
    /// </summary>
    private double ResolveResumeSeconds()
    {
        var published = Math.Max(
            Math.Max(_lastPublishedSeconds, _pinnedPosition),
            _pendingStartSeconds);
        var raw = ReadVlcSeconds();
        if (raw > 1 && published > 1 && Math.Abs(raw - published) <= 4)
            return raw;
        if (published > 1)
            return published;
        return Math.Max(raw, 0);
    }

    private string EnsurePlayProxy(string url)
    {
        if (_authProxy is { LocalUrl: not null }
            && _authProxy.IsHls == _pendingIsHls
            && string.Equals(_authProxy.TargetUrl, url, StringComparison.Ordinal))
        {
            if (_pendingIsHls)
                _authProxy.TryPrepareHlsMaster(_pendingHlsAudioTrackIndex);
            return _authProxy.LocalUrl;
        }

        _authProxy?.Dispose();
        _authProxy = new VlcAuthProxy(_pendingAuthorization);
        if (_authProxy.TryStart(url) && !string.IsNullOrEmpty(_authProxy.LocalUrl))
        {
            if (_pendingIsHls)
                _authProxy.TryPrepareHlsMaster(_pendingHlsAudioTrackIndex);
            return _authProxy.LocalUrl;
        }

        _authProxy.Dispose();
        _authProxy = null;
        return AppendAccessToken(url, _pendingAuthorization);
    }

    private static void AddHttpExtraHeader(Media media, string? authorizationHeader)
    {
        if (string.IsNullOrEmpty(authorizationHeader))
            return;

        media.AddOption(":http-extra-header=" + authorizationHeader);
    }

    private void StopPlayback(bool keepSession = false)
    {
        if (_player is null)
            return;

        try
        {
            _player.Stop();
        }
        catch
        {
        }

        _player.Media = null;
        _media?.Dispose();
        _media = null;
        TearDownHlsAudioEngine();
        if (keepSession)
            return;

        _authProxy?.Dispose();
        _authProxy = null;
    }

    private void Hook(MediaPlayer player)
    {
        player.Playing += OnPlaying;
        player.Paused += OnPaused;
        player.Stopped += OnStopped;
        player.EncounteredError += OnEncounteredError;
        player.TimeChanged += OnTimeChanged;
        player.LengthChanged += OnLengthChanged;
        player.Vout += OnVout;
        player.ESAdded += OnEsAdded;
    }

    private void Unhook(MediaPlayer player)
    {
        player.Playing -= OnPlaying;
        player.Paused -= OnPaused;
        player.Stopped -= OnStopped;
        player.EncounteredError -= OnEncounteredError;
        player.TimeChanged -= OnTimeChanged;
        player.LengthChanged -= OnLengthChanged;
        player.Vout -= OnVout;
        player.ESAdded -= OnEsAdded;
    }

    private void OnPlaying(object? sender, EventArgs e) =>
        Post(() =>
        {
            if (!_active)
                return;

            // Arm wall-clock only once demux is live so HLS does not fake a
            // running timer over a frozen frame.
            if (_holdTransport && !_holdWallArmed)
            {
                if (!_pendingIsHls || (_player is { Length: > 0 }))
                {
                    _holdWallArmed = true;
                    _holdStartedUtc = DateTime.UtcNow;
                }
            }

            ApplyAspectCore();
            ApplyOutputLevel();
            if (_pendingIsHls && _player is { Length: > 0 })
                StartHlsAudioSidecar();
            if (!_pendingIsHls)
                BindPendingTracksIfNeeded();

            // Direct Play: Playing fires before D3D11 presents (HEVC seek/buffer).
            // Clearing the veil here shows a black surface with a running overlay clock.
            // Wait for OnVout / OnD3d11FirstPresented for the visual first frame.
            if (_pendingIsHls)
                RaiseFirstFrame();
            else
                ScheduleDirectFirstFrameFallback();
            Playing?.Invoke();
            if (_firstFrameRaised || _pendingIsHls)
                StartClockTick();
        });

    private void ScheduleDirectFirstFrameFallback()
    {
        var playId = _clockTickId;
        PostDelayed(8_000, () =>
        {
            if (!_active || _firstFrameRaised || playId != _clockTickId)
                return;

            VlcPlayerLog.Warn("vlc direct first-frame fallback after 8s");
            RaiseFirstFrame();
            StartClockTick();
        });
    }

    private void OnPaused(object? sender, EventArgs e) =>
        Post(() =>
        {
            if (_active)
                Paused?.Invoke();
        });

    private void OnStopped(object? sender, EventArgs e) =>
        Post(() =>
        {
            if (_active && !_suppressEnded)
                Ended?.Invoke();
        });

    private void OnEncounteredError(object? sender, EventArgs e) =>
        Post(() =>
        {
            if (!_active)
                return;

            var detail = string.IsNullOrEmpty(_lastNativeError)
                ? (_media?.Mrl ?? "vlc-error")
                : _lastNativeError;
            VlcPlayerLog.Warn("vlc error mrl=" + VlcPlayerLog.SummarizeUrl(_media?.Mrl) + " native=" + detail);
            EncounteredError?.Invoke(detail);
        });

    private void OnTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e)
    {
        // Direct Play overlay clock is the playing wall-clock (Time/Position are
        // unreliable after :start-time reopen). Still use TimeChanged for HLS resume.
        if (_pendingIsHls && !_hlsResumeApplied)
        {
            Post(() =>
            {
                if (_active)
                    TryApplyHlsResume();
            });
        }
    }

    private void OnLengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e)
    {
        if (_pendingIsHls && e.Length > 0)
            Post(StartHlsAudioSidecar);

        if (!VlcTime.IsReliableLength(e.Length, _pinnedDuration))
            return;

        NoteVlcTimeScale(e.Length);
        var seconds = VlcTime.ToSeconds(e.Length, _ticksPerSecond);
        if (seconds <= 1)
            return;

        if (!VlcTime.TryAcceptDuration(seconds, _pinnedDuration, out seconds))
            return;

        if (_pinnedDuration > 1)
        {
            // Keep metadata total; only adopt VLC Length when it clearly matches.
            if (seconds >= _pinnedDuration * 0.9 && seconds <= _pinnedDuration * 1.1)
                _pinnedDuration = Math.Max(_pinnedDuration, seconds);
            Post(() =>
            {
                if (_active)
                    DurationChanged?.Invoke(_pinnedDuration);
            });
            return;
        }

        _pinnedDuration = seconds;
        Post(() =>
        {
            if (!_active)
                return;

            DurationChanged?.Invoke(seconds);
            PublishTransportFromPlayer();
        });
    }

    private void OnVout(object? sender, MediaPlayerVoutEventArgs e)
    {
        if (e.Count <= 0)
            return;

        Post(() =>
        {
            if (!_active)
                return;

            ApplyAspectCore();
            RaiseFirstFrame();
        });
    }

    private void OnEsAdded(object? sender, EventArgs e) =>
        Post(() =>
        {
            if (_active)
                BindPendingTracksIfNeeded();
        });

    private void RaiseFirstFrame()
    {
        if (_firstFrameRaised)
            return;

        // HLS: never clear the veil / run the overlay clock without a demux timeline.
        if (_pendingIsHls && _player is { Length: <= 0 })
        {
            BindPendingTracksIfNeeded();
            if (!_hlsResumeApplied && !TryApplyHlsResume())
                ScheduleHlsResumeRetry();
            return;
        }

        _firstFrameRaised = true;
        ApplyOutputLevel();
        if (_pendingIsHls)
            StartHlsAudioSidecar();
        BindPendingTracksIfNeeded();
        PublishTransportFromPlayer();
        NotifyFirstFrame();
        StartClockTick();
        if (!TryApplyHlsResume())
            ScheduleHlsResumeRetry();
    }

    /// <summary>
    /// LibVLC 4 adaptive often ignores #EXT-X-START. SetTime before the fMP4 demux
    /// exists treats init.m4s as a seekable MP4 (empty moov / no audio ES). Wait for
    /// a real VLC Length, seek at most once, then wait for position.
    /// </summary>
    private bool TryApplyHlsResume()
    {
        if (_hlsResumeApplied || _player is null)
            return true;

        if (!_pendingIsHls || _pendingStartSeconds <= 1)
        {
            _hlsResumeApplied = true;
            return true;
        }

        // Use the live VLC timeline only - never metadata DurationSeconds pin.
        var position = ReadRawVlcTimelineSeconds();
        if (position + 15 >= _pendingStartSeconds)
        {
            _hlsResumeApplied = true;
            return true;
        }

        if (_player.Length <= 0)
            return false;

        // EXT-X-START may already have placed playback near the target (GOP snap).
        if (position > 1)
        {
            _hlsResumeApplied = true;
            return true;
        }

        // Prefer playlist #EXT-X-START (same as Android). One late SetTime only
        // when demux is live and still sitting at ~0.
        if (!_hlsResumeSeekIssued)
        {
            _hlsResumeSeekIssued = true;
            _player.SetTime(VlcTime.FromSeconds(_pendingStartSeconds, _ticksPerSecond));
            VlcPlayerLog.Info(
                "vlc hls resume after demux seek="
                + _pendingStartSeconds.ToString("F1", CultureInfo.InvariantCulture)
                + "s lengthRaw="
                + _player.Length.ToString(CultureInfo.InvariantCulture));
        }

        return false;
    }

    private double ReadRawVlcTimelineSeconds()
    {
        if (_player is null)
            return 0;

        if (_player.Length > 0)
            NoteVlcTimeScale(_player.Length);

        if (_player.Time > 0)
            return VlcTime.ToSeconds(_player.Time, _ticksPerSecond);

        if (_player.Length > 0 && _player.Position > 0)
            return _player.Position * VlcTime.ToSeconds(_player.Length, _ticksPerSecond);

        return 0;
    }

    private void ScheduleHlsResumeRetry()
    {
        PostDelayed(400, () =>
        {
            if (!_active)
                return;

            if (_hlsResumeApplied)
            {
                if (!_firstFrameRaised && _player is { Length: > 0 })
                    RaiseFirstFrame();
                return;
            }

            if (TryApplyHlsResume())
            {
                if (!_firstFrameRaised && _player is { Length: > 0 })
                    RaiseFirstFrame();
                return;
            }

            _hlsResumeAttempts++;
            if (_hlsResumeAttempts < 40)
            {
                ScheduleHlsResumeRetry();
                return;
            }

            _hlsResumeApplied = true;
            if (_player is { Length: > 0 })
            {
                VlcPlayerLog.Warn(
                    "vlc hls resume give up start="
                    + _pendingStartSeconds.ToString("F1", CultureInfo.InvariantCulture)
                    + "s");
                if (!_firstFrameRaised)
                    RaiseFirstFrame();
                return;
            }

            VlcPlayerLog.Warn("vlc hls demux failed length=0");
            EncounteredError?.Invoke("hls-demux-failed");
        });
    }

    private void StartClockTick()
    {
        var id = ++_clockTickId;
        PostDelayed(100, () => TickOverlayClock(id));
    }

    private void TickOverlayClock(int id)
    {
        if (!_active || id != _clockTickId || _player is null)
            return;

        if (_player.IsPlaying)
        {
            var seconds = ReadVlcSeconds();
            if (_holdTransport)
            {
                seconds = VlcTime.FollowAfterReopen(
                    seconds,
                    _pinnedPosition,
                    _holdStartedUtc,
                    _rate,
                    _firstFrameRaised && _holdWallArmed,
                    ref _holdTransport);
            }

            if (seconds >= 0 && (seconds > 0 || _holdTransport || _pendingStartSeconds <= 1))
            {
                _lastPublishedSeconds = seconds;
                PositionChanged?.Invoke(seconds);
            }

            // Direct Play: never promote Playing+clock into "first frame" - wait for vout.
            if (!_firstFrameRaised && _pendingIsHls)
                RaiseFirstFrame();
        }
        else if (_holdTransport)
        {
            // Freeze while buffering/paused so cues do not drift ahead of audio.
            var held = _lastPublishedSeconds > 0 ? _lastPublishedSeconds : _pinnedPosition;
            PositionChanged?.Invoke(held);
        }

        PostDelayed(100, () => TickOverlayClock(id));
    }

    private void NoteVlcTimeScale(long lengthTicks)
    {
        if (!VlcTime.IsReliableLength(lengthTicks, _pinnedDuration))
            return;

        var detected = VlcTime.DetectTicksPerSecond(lengthTicks, _pinnedDuration);
        if (detected == _ticksPerSecond && _ticksScaleLogged)
            return;

        _ticksPerSecond = detected;
        if (_ticksScaleLogged)
            return;

        _ticksScaleLogged = true;
        VlcPlayerLog.Info(
            "vlc time scale="
            + (_ticksPerSecond == VlcTime.MillisecondsPerSecond ? "ms" : "us")
            + " lengthRaw="
            + lengthTicks.ToString(CultureInfo.InvariantCulture)
            + " knownDuration="
            + _pinnedDuration.ToString("F1", CultureInfo.InvariantCulture)
            + "s");
    }

    private double ReadVlcDurationSeconds()
    {
        if (_player is not { Length: > 0 } player)
            return 0;

        NoteVlcTimeScale(player.Length);
        return VlcTime.ToSeconds(player.Length, _ticksPerSecond);
    }

    private double ReadVlcSeconds()
    {
        if (_player is null)
            return Math.Max(0, _lastPublishedSeconds);

        if (_player.Length > 0)
            NoteVlcTimeScale(_player.Length);

        // Prefer Position * duration: after :start-time, Position stays on the
        // absolute media timeline while Time often restarts at 0 (relative).
        var duration = _pinnedDuration > 1 ? _pinnedDuration : ReadVlcDurationSeconds();
        if (duration > 1 && _player.Position > 0)
        {
            var fromPos = _player.Position * duration;
            if (fromPos >= 0.5)
                return fromPos;
        }

        if (_player.Time > 0)
            return VlcTime.ToSeconds(_player.Time, _ticksPerSecond);

        return 0;
    }

    private void PublishTransportFromPlayer()
    {
        var duration = _pinnedDuration > 1 ? _pinnedDuration : ReadVlcDurationSeconds();
        if (duration > 1)
        {
            _pinnedDuration = Math.Max(_pinnedDuration, duration);
            DurationChanged?.Invoke(_pinnedDuration);
        }

        var seconds = ReadVlcSeconds();
        if (_holdTransport)
        {
            seconds = VlcTime.FollowAfterReopen(
                seconds,
                _pinnedPosition,
                _holdStartedUtc,
                _rate,
                firstFrameSeen: _firstFrameRaised && _holdWallArmed,
                ref _holdTransport);
        }

        _lastPublishedSeconds = seconds;
        if (seconds > 0 || _holdTransport)
            PositionChanged?.Invoke(seconds);

        if (!_ticksScaleLogged && _player is not null)
        {
            VlcPlayerLog.Info(
                "vlc transport timeRaw="
                + _player.Time.ToString(CultureInfo.InvariantCulture)
                + " lengthRaw="
                + _player.Length.ToString(CultureInfo.InvariantCulture)
                + " pos="
                + _player.Position.ToString("F4", CultureInfo.InvariantCulture)
                + " -> "
                + seconds.ToString("F1", CultureInfo.InvariantCulture)
                + "s / "
                + duration.ToString("F1", CultureInfo.InvariantCulture)
                + "s");
        }
    }

    private void NotifyFirstFrame()
    {
        if (_firstFrameNotified)
            return;

        _firstFrameNotified = true;
        FirstFrame?.Invoke();
    }

    private void OnD3d11FirstPresented() =>
        Post(() =>
        {
            if (_active)
                RaiseFirstFrame();
        });

    private void ApplyOutputLevel()
    {
        // LibVLC 100 = 0dB. WebView2/Video.js at the same IPlayerService level measures
        // roughly twice as loud on Windows, so map UI 0-1 onto LibVLC 0-200 (+6dB at full).
        var volume = (int)Math.Clamp(_volume01 * 200.0, 0, 200);
        if (_player is not null)
        {
            // HLS: video player is :no-audio; sidecar owns mmdevice gain.
            if (!_pendingIsHls)
            {
                _player.SetVolume(volume);
                _player.Mute = _muted;
            }
            else
            {
                _player.SetVolume(0);
                _player.Mute = true;
            }
        }

        if (_hlsAudio is not null)
            _hlsAudio.SetVolume(_volume01, _muted);

        // Do not touch WASAPI here - mmdevice owns the "K7" session. Fighting it
        // (or layering VolumeService on top) made Direct quieter than Video.js.
    }

    private void BindPendingTracksIfNeeded()
    {
        if (_player is null)
            return;

        if (_pendingIsHls)
        {
            // Demuxed audio is the WinRT sidecar - no ES on the video player.
            if (_hlsAudio is null || !_hlsAudio.IsActive)
                StartHlsAudioSidecar();
            if (_overlayOwnsTextSubs)
                _player.Unselect(TrackType.Text);
            return;
        }

        BindAudio();
        if (_overlayOwnsTextSubs)
        {
            _player.Unselect(TrackType.Text);
            return;
        }

        BindSubtitle();
    }

    private void BindHlsAudioIfNeeded()
    {
        if (_player is null)
            return;

        if (_player.Length <= 0)
        {
            if (_audioBindAttempts < 24)
            {
                _audioBindAttempts++;
                PostDelayed(400, BindHlsAudioIfNeeded);
            }

            return;
        }

        StartHlsAudioSidecar();
        TryApplyHlsResume();
    }

    private void BindAudio()
    {
        if (_player is null)
            return;

        var tracks = VlcTracks.Snapshot(_player, TrackType.Audio);
        try
        {
            if (tracks.Length == 0)
            {
                if (_audioBindAttempts < 16)
                {
                    _audioBindAttempts++;
                    PostDelayed(250, BindPendingTracksIfNeeded);
                }

                return;
            }

            var ordinal = _pendingAudioOrdinal;
            if (!VlcTracks.TryResolve(tracks, ordinal, null, null, out var index, out var track))
            {
                if (VlcTracks.SelectedId(_player, TrackType.Audio) is not null)
                    return;

                index = 0;
                track = tracks[0];
                _pendingAudioOrdinal = 0;
            }

            if (track.Selected)
                return;

            _player.Select(track);
            VlcPlayerLog.Info(
                "vlc audio bind id="
                + (track.Id ?? "-")
                + " name="
                + (track.Name ?? "-")
                + " ordinal="
                + index);
        }
        finally
        {
            VlcTracks.DisposeAll(tracks);
        }
    }

    private void BindSubtitle()
    {
        if (_player is null || _pendingSubtitleOrdinal is null)
            return;

        var tracks = VlcTracks.Snapshot(_player, TrackType.Text);
        try
        {
            if (!VlcTracks.TryResolve(tracks, _pendingSubtitleOrdinal, null, null, out _, out var track))
                return;

            if (track.Selected)
                return;

            _player.Select(track);
            VlcPlayerLog.Info("vlc sub bind id=" + (track.Id ?? "-") + " name=" + (track.Name ?? "-"));
        }
        finally
        {
            VlcTracks.DisposeAll(tracks);
        }
    }

    private void PostDelayed(int delayMs, Action action)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(delayMs);
            Post(action);
        });
    }

    private void ApplyAspectCore()
    {
        if (_player is null)
            return;

        switch (_aspect)
        {
            case AspectRatioMode.Stretch:
                var width = (int)_videoView.Width;
                var height = (int)_videoView.Height;
                _player.Scale = 0;
                _player.AspectRatio = width > 0 && height > 0
                    ? width.ToString(CultureInfo.InvariantCulture)
                      + ":"
                      + height.ToString(CultureInfo.InvariantCulture)
                    : null;
                break;
            case AspectRatioMode.Fill:
                _player.AspectRatio = null;
                _player.Scale = ComputeFillScale();
                break;
            default:
                _player.AspectRatio = null;
                _player.Scale = 0;
                break;
        }
    }

    private float ComputeFillScale()
    {
        if (_player is null)
            return 0;

        uint videoW = 0;
        uint videoH = 0;
        if (!_player.Size(0, ref videoW, ref videoH) || videoW == 0 || videoH == 0)
            return 0;

        var viewW = _videoView.Width;
        var viewH = _videoView.Height;
        if (viewW <= 0 || viewH <= 0)
            return 0;

        var scaleX = viewW / videoW;
        var scaleY = viewH / videoH;
        return (float)Math.Max(scaleX, scaleY);
    }

    private void OnLibVlcLog(object? sender, LogEventArgs e)
    {
        var module = e.Module ?? "-";
        var message = e.Message ?? "";
        if (IsNoisyVlcLog(message))
            return;

        // HLS adaptive stalls with Length=0: surface Info from demux/http too.
        var hlsDiag = _engineIsHls
            && e.Level is LogLevel.Error or LogLevel.Warning or LogLevel.Notice
            && (module.Contains("adaptive", StringComparison.OrdinalIgnoreCase)
                || module.Contains("hls", StringComparison.OrdinalIgnoreCase)
                || module.Contains("mp4", StringComparison.OrdinalIgnoreCase)
                || module.Contains("http", StringComparison.OrdinalIgnoreCase)
                || module.Contains("demux", StringComparison.OrdinalIgnoreCase));

        if (e.Level is not (LogLevel.Error or LogLevel.Warning) && !hlsDiag)
            return;

        if (message.Length > 180)
            message = message[..180];

        if (e.Level == LogLevel.Error)
            _lastNativeError = module + " " + message;

        VlcPlayerLog.Warn("vlc-native " + module + " " + message);
    }

    private static bool IsNoisyVlcLog(string message) =>
        message.Contains("picture is too late", StringComparison.Ordinal)
        || message.Contains("More than 11 late frames", StringComparison.Ordinal)
        || message.Contains("not implemented", StringComparison.Ordinal)
        || message.Contains("invalid stop-time", StringComparison.Ordinal)
        || message.Contains("playback too early", StringComparison.Ordinal)
        || message.Contains("playback too late", StringComparison.Ordinal)
        || message.Contains("down-sampling", StringComparison.Ordinal)
        || message.Contains("up-sampling", StringComparison.Ordinal);

    private static string AppendAccessToken(string url, string? authorizationHeader)
    {
        if (string.IsNullOrEmpty(authorizationHeader))
            return url;

        var token = authorizationHeader;
        const string bearer = "Bearer ";
        if (token.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
            token = token[bearer.Length..];

        if (url.Contains("access_token=", StringComparison.OrdinalIgnoreCase)
            || url.Contains("ephemeral_token=", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return url + separator + "access_token=" + Uri.EscapeDataString(token);
    }

    private static void Post(Action action)
    {
        if (MainThread.IsMainThread)
            action();
        else
            MainThread.BeginInvokeOnMainThread(action);
    }
}
#endif
