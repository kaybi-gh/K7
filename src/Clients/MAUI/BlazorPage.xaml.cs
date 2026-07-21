using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Shared.Interfaces;
using Microsoft.JSInterop;

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

    public BlazorPage(IPlayerService playerService, IAudioPlayerService audioPlayerService, BackButtonService backButtonService, IK7ServerService k7ServerService)
    {
        _playerService = playerService;
        _audioPlayerService = audioPlayerService;
        _backButtonService = backButtonService;
        _k7ServerService = k7ServerService;
        InitializeComponent();
#if WINDOWS
        SyncWindowsStreamAuthContext();
#endif
        blazorWebView.WebResourceRequested += OnWebResourceRequested;
        InitializeSplashOverlay();
        InitializePlayer();
        InitializeAudioPlayer();
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
                SplashOverlay.IsVisible = false;
                RootGrid.Children.Remove(SplashOverlay);
            });
        });
    }

    protected override bool OnBackButtonPressed()
    {
        HandleBackButton();
        return true;
    }

    internal void HandleBackButton()
    {
        if (_backButtonService.HandleBackButton())
            return;

        // When native video player is active, signal the overlay component
        if (_playerService.IsVisible)
        {
            System.Diagnostics.Debug.WriteLine("[K7-Player] BlazorPage.HandleBackButton -> PlayerService.OnBackPressed");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (_playerService is MAUI.Services.PlayerService ps)
                    ps.OnBackPressed();
            });
            return;
        }

        DispatchBackAsEscape();
    }

    internal void DispatchBackAsEscape()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
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
            _ = blazorWebView.TryDispatchAsync(async sp =>
            {
                try
                {
                    var js = sp.GetRequiredService<IJSRuntime>();
                    await js.InvokeVoidAsync("K7.onTvRemoteSelect", phase, keyCode, heldMs);
                }
                catch (JSException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            });
        });
    }

    internal void HandleMediaPlayPause()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
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

    private Task HandleVideoSeekRequested(double position)
    {
        return SeekMediaElementAsync(
            NativePlayer,
            TimeSpan.FromSeconds(position),
            () => _playerService.PlaybackState,
            t => _playerService.CurrentTime = t);
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
            _playerService.PlaybackState = NativePlayer.CurrentState switch
            {
                MediaElementState.Buffering => Server.Domain.Enums.PlaybackState.Buffering,
                MediaElementState.Playing => Server.Domain.Enums.PlaybackState.Playing,
                MediaElementState.Paused => Server.Domain.Enums.PlaybackState.Paused,
                MediaElementState.Opening => Server.Domain.Enums.PlaybackState.Idle,
                MediaElementState.Stopped => Server.Domain.Enums.PlaybackState.Idle,
                _ => Server.Domain.Enums.PlaybackState.Unknown,
            };
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
        });
#endif
    }

#if !WINDOWS
    private void OpenNativePlayerSource(PlayerSource source)
    {
        ClearNativePlayerMediaSurface();
        NativePlayer.ShouldAutoPlay = true;
        NativePlayer.Source = CreateMediaSourceWithAuth(source.Url!);
        NativePlayer.Play();
        AttachPendingSeekHandler(source);
    }

    private void AttachPendingSeekHandler(PlayerSource source)
    {
        if (source.PendingSeekTime is not double seekTime)
            return;

        // Seek once the player starts playing (MediaOpened may not fire on source swap)
        void OnStateChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(MediaElement.CurrentState))
                return;

            if (NativePlayer.CurrentState is MediaElementState.Playing or MediaElementState.Buffering)
            {
                NativePlayer.PropertyChanged -= OnStateChanged;
                SeekMediaElementAsync(
                    NativePlayer,
                    TimeSpan.FromSeconds(seekTime),
                    () => _playerService.PlaybackState,
                    t => _playerService.CurrentTime = t).FireAndForget();
            }
        }

        NativePlayer.PropertyChanged += OnStateChanged;
    }
#endif

    private void OnIsVisibleChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
#if WINDOWS
            System.Diagnostics.Debug.WriteLine(
                _playerService.IsVisible
                    ? "[K7-Player] IsVisible=true, configuring Windows layout"
                    : "[K7-Player] IsVisible=false, restoring Windows layout");
            ConfigureWindowsVideoPlayerLayout();
#else
            NativePlayer.IsVisible = _playerService.IsVisible;

            if (_playerService.IsVisible)
            {
                BackgroundColor = Colors.Black;
                Padding = new Thickness(0);
#if ANDROID || IOS
                DeviceDisplay.Current.KeepScreenOn = true;
                Microsoft.Maui.Devices.DeviceDisplay.Current.MainDisplayInfoChanged += OnDisplayInfoChanged;
                SetLandscapeOrientation();
#endif
            }
            else
            {
                BackgroundColor = Colors.Transparent;
                NativePlayer.Stop();
                NativePlayer.Source = null;
#if ANDROID || IOS
                DeviceDisplay.Current.KeepScreenOn = false;
                Microsoft.Maui.Devices.DeviceDisplay.Current.MainDisplayInfoChanged -= OnDisplayInfoChanged;
                RestoreOrientation();
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
        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Idle;
#endif
    }

    private void NativePlayer_MediaEnded(object? sender, EventArgs e)
    {
#if !WINDOWS
        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Ended;
#endif
    }

    private void NativePlayer_MediaFailed(object? sender, MediaFailedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[K7-Player] Media playback failed: {e.ErrorMessage}");

        if (_playerService is not Services.PlayerService playerService)
            return;

#if WINDOWS
        return;
#else
        MainThread.BeginInvokeOnMainThread(() =>
            HandleNativePlayerMediaFailedAsync(playerService).FireAndForget());
#endif
    }

#if !WINDOWS
    private async Task HandleNativePlayerMediaFailedAsync(Services.PlayerService playerService)
    {
        ClearNativePlayerMediaSurface();

        var recovered = await playerService.TryRecoverPlaybackStartAsync();
        if (!recovered)
            await playerService.AbortPlaybackStartAsync();
    }

    private void ClearNativePlayerMediaSurface()
    {
        NativePlayer.Stop();
        NativePlayer.Source = null;
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
        NativeAudioPlayer.Stop();
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
#if ANDROID || IOS
        // On Android/iOS, native services (K7MediaLibraryService / NativeAudioService)
        // handle audio playback directly. Skip MediaElement wiring.
        return;
#else
        NativeAudioPlayer.Volume = _audioPlayerService.Volume;
        NativeAudioPlayer.ShouldMute = _audioPlayerService.IsMuted;

        NativeAudioPlayer.PositionChanged += AudioPlayer_PositionChanged;
        NativeAudioPlayer.MediaEnded += AudioPlayer_MediaEnded;
        NativeAudioPlayer.MediaFailed += AudioPlayer_MediaFailed;
        NativeAudioPlayer.PropertyChanged += NativeAudioPlayer_PropertyChanged;

        _audioPlayerService.CurrentTrackChanged += OnAudioCurrentTrackChanged;
        _audioPlayerService.SourceChanged += OnAudioSourceChanged;
        _audioPlayerService.PlayRequested += HandleAudioPlayRequested;
        _audioPlayerService.PauseRequested += HandleAudioPauseRequested;
        _audioPlayerService.StopRequested += HandleAudioStopRequested;
        _audioPlayerService.SeekRequested += HandleAudioSeekRequested;
        _audioPlayerService.MuteRequested += HandleAudioMuteRequested;
        _audioPlayerService.UnmuteRequested += HandleAudioUnmuteRequested;
        _audioPlayerService.VolumeChangeRequested += HandleAudioVolumeChangeRequested;
#endif
    }

    private Task HandleAudioPlayRequested()
    {
        MainThread.BeginInvokeOnMainThread(NativeAudioPlayer.Play);
        return Task.CompletedTask;
    }

    private Task HandleAudioPauseRequested()
    {
        MainThread.BeginInvokeOnMainThread(NativeAudioPlayer.Pause);
        return Task.CompletedTask;
    }

    private Task HandleAudioStopRequested()
    {
        MainThread.BeginInvokeOnMainThread(NativeAudioPlayer.Stop);
        return Task.CompletedTask;
    }

    private Task HandleAudioSeekRequested(double position) =>
        SeekMediaElementAsync(
            NativeAudioPlayer,
            TimeSpan.FromSeconds(position),
            () => _audioPlayerService.PlaybackState,
            t => _audioPlayerService.CurrentTime = t);

    private Task HandleAudioMuteRequested()
    {
        MainThread.BeginInvokeOnMainThread(() => NativeAudioPlayer.ShouldMute = true);
        return Task.CompletedTask;
    }

    private Task HandleAudioUnmuteRequested()
    {
        MainThread.BeginInvokeOnMainThread(() => NativeAudioPlayer.ShouldMute = false);
        return Task.CompletedTask;
    }

    private Task HandleAudioVolumeChangeRequested(double volume)
    {
        MainThread.BeginInvokeOnMainThread(() => NativeAudioPlayer.Volume = volume);
        return Task.CompletedTask;
    }

    private void NativeAudioPlayer_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MediaElement.Duration))
        {
            var duration = NativeAudioPlayer.Duration.TotalSeconds;
            if (duration > 0 && duration != _audioPlayerService.Duration)
                _audioPlayerService.Duration = duration;
        }

        if (e.PropertyName == nameof(MediaElement.CurrentState))
        {
            _audioPlayerService.PlaybackState = NativeAudioPlayer.CurrentState switch
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
#if !WINDOWS
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

#if !ANDROID && !IOS
        NativeAudioPlayer.PositionChanged -= AudioPlayer_PositionChanged;
        NativeAudioPlayer.MediaEnded -= AudioPlayer_MediaEnded;
        NativeAudioPlayer.MediaFailed -= AudioPlayer_MediaFailed;
        NativeAudioPlayer.PropertyChanged -= NativeAudioPlayer_PropertyChanged;

        _audioPlayerService.CurrentTrackChanged -= OnAudioCurrentTrackChanged;
        _audioPlayerService.SourceChanged -= OnAudioSourceChanged;
        _audioPlayerService.PlayRequested -= HandleAudioPlayRequested;
        _audioPlayerService.PauseRequested -= HandleAudioPauseRequested;
        _audioPlayerService.StopRequested -= HandleAudioStopRequested;
        _audioPlayerService.SeekRequested -= HandleAudioSeekRequested;
        _audioPlayerService.MuteRequested -= HandleAudioMuteRequested;
        _audioPlayerService.UnmuteRequested -= HandleAudioUnmuteRequested;
        _audioPlayerService.VolumeChangeRequested -= HandleAudioVolumeChangeRequested;
#endif

        DetachPlayerPlatform();
    }

    partial void DetachPlayerPlatform();

    private void OnAudioCurrentTrackChanged(AudioQueueItem? track)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (track is null) return;

            NativeAudioPlayer.MetadataTitle = track.Title;
            NativeAudioPlayer.MetadataArtist = track.Artist ?? "";
            NativeAudioPlayer.MetadataArtworkUrl = _k7ServerService.GetAbsoluteUri(track.CoverUrl)?.AbsoluteUri ?? "";
        });
    }

    private void OnAudioSourceChanged(PlayerSource source)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!string.IsNullOrEmpty(source.Url))
            {
                NativeAudioPlayer.Source = CreateMediaSourceWithAuth(source.Url);
                NativeAudioPlayer.Play();
            }
        });
    }

    private MediaSource CreateMediaSourceWithAuth(string url)
    {
        if (File.Exists(url))
        {
            return MediaSource.FromFile(url);
        }

        var authHeader = _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization;
        if (authHeader is not null)
        {
            var headers = new Dictionary<string, string>
            {
                ["Authorization"] = authHeader.ToString()
            };
            return MediaSource.FromUri(new Uri(url), headers);
        }

        return MediaSource.FromUri(url);
    }

    private void AudioPlayer_PositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
        _audioPlayerService.CurrentTime = e.Position.TotalSeconds;
    }

    private void AudioPlayer_MediaEnded(object? sender, EventArgs e) => _audioPlayerService.OnTrackEndedAsync().FireAndForget();

    private void AudioPlayer_MediaFailed(object? sender, MediaFailedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[K7-Audio] Playback failed: {e.ErrorMessage}");
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

#if WINDOWS
    partial void ConfigureWindowsVideoPlayerLayout();

    private void SyncWindowsStreamAuthContext() =>
        Platforms.Windows.WindowsStreamAuthContext.UpdateFrom(_k7ServerService);
#endif

    private static Task SeekMediaElementAsync(
        MediaElement mediaElement,
        TimeSpan position,
        Func<Server.Domain.Enums.PlaybackState>? getPlaybackState = null,
        Action<double>? setCurrentTime = null) =>
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[K7-Player] Seek failed at {target}: {ex.Message}");
            }

            setCurrentTime?.Invoke(target.TotalSeconds);

            if (resumePlayback
                && mediaElement.CurrentState is not MediaElementState.Playing
                && mediaElement.CurrentState is not MediaElementState.Buffering)
            {
                mediaElement.Play();
            }
        });
}
