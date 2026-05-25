using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using K7.Clients.Shared.Enums;
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

    private static readonly string DownloadsBasePath = Path.Combine(FileSystem.AppDataDirectory, "downloads");

    public BlazorPage(IPlayerService playerService, IAudioPlayerService audioPlayerService, BackButtonService backButtonService, IK7ServerService k7ServerService)
    {
        _playerService = playerService;
        _audioPlayerService = audioPlayerService;
        _backButtonService = backButtonService;
        _k7ServerService = k7ServerService;
        InitializeComponent();
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
        var filePath = Path.Combine(DownloadsBasePath, relativePath);

        if (!filePath.StartsWith(DownloadsBasePath, StringComparison.OrdinalIgnoreCase) || !File.Exists(filePath))
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
                var js = sp.GetRequiredService<IJSRuntime>();
                await js.InvokeVoidAsync("SpatialNav.handleBack");
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

        _playerService.SourceChanged += OnSourceChanged;
        _playerService.IsVisibleChanged += OnIsVisibleChanged;
        _playerService.PlayRequested += () => { NativePlayer.Play(); return Task.CompletedTask; };
        _playerService.PauseRequested += () => { NativePlayer.Pause(); return Task.CompletedTask; };
        _playerService.MuteRequested += () => { NativePlayer.ShouldMute = true; return Task.CompletedTask; };
        _playerService.UnmuteRequest += () => { NativePlayer.ShouldMute = false; return Task.CompletedTask; };
        _playerService.VolumeChangeRequested += (volume) => { NativePlayer.Volume = volume; return Task.CompletedTask; };
        _playerService.PlaybackRateChangeRequested += (rate) => { NativePlayer.Speed = rate; return Task.CompletedTask; };
        _playerService.StopRequested += () => { NativePlayer.Stop(); return Task.CompletedTask; };
        _playerService.SeekRequested += (position) => { NativePlayer.SeekTo(TimeSpan.FromSeconds(position)); return Task.CompletedTask; };
        _playerService.AspectRatioModeChangeRequested += OnAspectRatioModeChanged;
        InitializePlayerPlatform();

        NativePlayer.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(MediaElement.Duration))
            {
                var duration = NativePlayer.Duration.TotalSeconds;
                if (duration > 0 && duration != _playerService.Duration)
                {
                    _playerService.Duration = duration;
                }
            }
            if (e.PropertyName == nameof(MediaElement.ShouldMute))
            {
                _playerService.IsMuted = NativePlayer.ShouldMute;
            }
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
        };
    }

    private void OnSourceChanged(PlayerSource source)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!string.IsNullOrEmpty(source.Url))
            {
                NativePlayer.Stop();
                NativePlayer.ShouldAutoPlay = true;
                NativePlayer.Source = CreateMediaSourceWithAuth(source.Url);
                NativePlayer.Play();

                if (source.PendingSeekTime is double seekTime)
                {
                    // Seek once the player starts playing (MediaOpened may not fire on source swap)
                    void OnStateChanged(object? s, System.ComponentModel.PropertyChangedEventArgs e)
                    {
                        if (e.PropertyName != nameof(MediaElement.CurrentState))
                            return;

                        if (NativePlayer.CurrentState is MediaElementState.Playing or MediaElementState.Buffering)
                        {
                            NativePlayer.PropertyChanged -= OnStateChanged;
                            NativePlayer.SeekTo(TimeSpan.FromSeconds(seekTime));
                        }
                    }

                    NativePlayer.PropertyChanged += OnStateChanged;
                }
            }
        });
    }

    private void OnIsVisibleChanged()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
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
#if WINDOWS
                // WebView2 transparency is broken on WinUI3 (microsoft-ui-xaml#6527).
                // Place MediaElement in front of the BlazorWebView and use native controls.
                NativePlayer.ZIndex = 3;
                NativePlayer.ShouldShowPlaybackControls = true;
                NativePlayer.InputTransparent = false;
                NativePlayerCloseButton.IsVisible = true;
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
#if WINDOWS
                NativePlayer.ZIndex = 1;
                NativePlayer.ShouldShowPlaybackControls = false;
                NativePlayer.InputTransparent = true;
                NativePlayerCloseButton.IsVisible = false;
#endif
            }
        });
    }

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

    private void NativePlayer_MediaOpened(object? sender, EventArgs e)
    {
        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Idle;
    }

    private void NativePlayer_MediaEnded(object? sender, EventArgs e)
    {
        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Ended;
    }

    private void NativePlayer_MediaFailed(object? sender, MediaFailedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[K7-Player] Media playback failed: {e.ErrorMessage}");
    }

    private void NativePlayer_PositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
        _playerService.CurrentTime = e.Position.TotalSeconds;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        NativePlayer.Stop();
        NativeAudioPlayer.Stop();
    }

    private async void OnNativePlayerCloseClicked(object? sender, EventArgs e)
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

        _audioPlayerService.CurrentTrackChanged += OnAudioCurrentTrackChanged;
        _audioPlayerService.SourceChanged += OnAudioSourceChanged;
        _audioPlayerService.PlayRequested += () => { MainThread.BeginInvokeOnMainThread(NativeAudioPlayer.Play); return Task.CompletedTask; };
        _audioPlayerService.PauseRequested += () => { MainThread.BeginInvokeOnMainThread(NativeAudioPlayer.Pause); return Task.CompletedTask; };
        _audioPlayerService.StopRequested += () => { MainThread.BeginInvokeOnMainThread(NativeAudioPlayer.Stop); return Task.CompletedTask; };
        _audioPlayerService.SeekRequested += (position) => { MainThread.BeginInvokeOnMainThread(() => NativeAudioPlayer.SeekTo(TimeSpan.FromSeconds(position))); return Task.CompletedTask; };
        _audioPlayerService.MuteRequested += () => { MainThread.BeginInvokeOnMainThread(() => NativeAudioPlayer.ShouldMute = true); return Task.CompletedTask; };
        _audioPlayerService.UnmuteRequested += () => { MainThread.BeginInvokeOnMainThread(() => NativeAudioPlayer.ShouldMute = false); return Task.CompletedTask; };
        _audioPlayerService.VolumeChangeRequested += (volume) => { MainThread.BeginInvokeOnMainThread(() => NativeAudioPlayer.Volume = volume); return Task.CompletedTask; };

        NativeAudioPlayer.PropertyChanged += (sender, e) =>
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
        };
#endif
    }

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

    private async void AudioPlayer_MediaEnded(object? sender, EventArgs e)
    {
        await _audioPlayerService.OnTrackEndedAsync();
    }

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
}
