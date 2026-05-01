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

    public BlazorPage(IPlayerService playerService, IAudioPlayerService audioPlayerService, BackButtonService backButtonService, IK7ServerService k7ServerService)
    {
        _playerService = playerService;
        _audioPlayerService = audioPlayerService;
        _backButtonService = backButtonService;
        _k7ServerService = k7ServerService;
        InitializeComponent();
        InitializePlayer();
        InitializeAudioPlayer();
    }

    protected override bool OnBackButtonPressed()
    {
        if (_backButtonService.HandleBackButton())
            return true;

        if (_playerService.IsVisible)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _playerService.Stop();
                _playerService.HideAsync();
            });
            return true;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            _ = blazorWebView.TryDispatchAsync(async sp =>
            {
                var js = sp.GetRequiredService<IJSRuntime>();
                await js.InvokeVoidAsync("SpatialNav.handleBack");
            });
        });
        return true;
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
                System.Diagnostics.Debug.WriteLine($"[K7-Player] Setting source: {source.Url}");
                System.Diagnostics.Debug.WriteLine($"[K7-Player] Volume={NativePlayer.Volume}, ShouldMute={NativePlayer.ShouldMute}");
                NativePlayer.ShouldAutoPlay = true;
                NativePlayer.Source = CreateMediaSourceWithAuth(source.Url);
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
                SafeAreaEdges = SafeAreaEdges.None;
                RootGrid.SafeAreaEdges = SafeAreaEdges.None;
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
                BackgroundColor = (Color)Application.Current!.Resources["PageBackgroundColor"];
                SafeAreaEdges = SafeAreaEdges.Default;
                RootGrid.SafeAreaEdges = SafeAreaEdges.Default;
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
        NativeAudioPlayer.Volume = _audioPlayerService.Volume;
        NativeAudioPlayer.ShouldMute = _audioPlayerService.IsMuted;

        NativeAudioPlayer.PositionChanged += AudioPlayer_PositionChanged;
        NativeAudioPlayer.MediaEnded += AudioPlayer_MediaEnded;
        NativeAudioPlayer.MediaFailed += AudioPlayer_MediaFailed;

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
    }

    private void OnAudioSourceChanged(PlayerSource source)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (!string.IsNullOrEmpty(source.Url))
                NativeAudioPlayer.Source = CreateMediaSourceWithAuth(source.Url);
        });
    }

    private MediaSource CreateMediaSourceWithAuth(string url)
    {
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
        var activity = Platform.CurrentActivity;
        if (activity is not null)
        {
            activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.SensorLandscape;

            var window = activity.Window;
            if (window is not null)
            {
#pragma warning disable CA1422, CS0618
                if (OperatingSystem.IsAndroidVersionAtLeast(30))
                {
                    window.SetDecorFitsSystemWindows(false);
                }
                window.SetStatusBarColor(Android.Graphics.Color.Transparent);
                window.SetNavigationBarColor(Android.Graphics.Color.Transparent);
                window.AddFlags(Android.Views.WindowManagerFlags.Fullscreen);
                window.AddFlags(Android.Views.WindowManagerFlags.LayoutNoLimits);

                if (OperatingSystem.IsAndroidVersionAtLeast(28))
                {
                    window.Attributes!.LayoutInDisplayCutoutMode =
                        Android.Views.LayoutInDisplayCutoutMode.ShortEdges;
                }

                window.DecorView.SystemUiFlags =
                    Android.Views.SystemUiFlags.Fullscreen
                    | Android.Views.SystemUiFlags.HideNavigation
                    | Android.Views.SystemUiFlags.ImmersiveSticky
                    | Android.Views.SystemUiFlags.LayoutFullscreen
                    | Android.Views.SystemUiFlags.LayoutHideNavigation
                    | Android.Views.SystemUiFlags.LayoutStable;
#pragma warning restore CA1422, CS0618
            }
        }
#endif
    }

    private static void RestoreOrientation()
    {
        DeviceDisplay.Current.KeepScreenOn = false;
#if ANDROID
        var activity = Platform.CurrentActivity;
        if (activity is not null)
        {
            activity.RequestedOrientation = Android.Content.PM.ScreenOrientation.Unspecified;

            var window = activity.Window;
            if (window is not null)
            {
#pragma warning disable CA1422, CS0618
                if (OperatingSystem.IsAndroidVersionAtLeast(30))
                {
                    window.SetDecorFitsSystemWindows(true);
                }
                window.ClearFlags(Android.Views.WindowManagerFlags.Fullscreen);
                window.ClearFlags(Android.Views.WindowManagerFlags.LayoutNoLimits);

                if (OperatingSystem.IsAndroidVersionAtLeast(28))
                {
                    window.Attributes!.LayoutInDisplayCutoutMode =
                        Android.Views.LayoutInDisplayCutoutMode.Default;
                }

                window.DecorView.SystemUiFlags = Android.Views.SystemUiFlags.Visible;
#pragma warning restore CA1422, CS0618
            }
        }
#endif
    }

    private void OnDisplayInfoChanged(object? sender, DisplayInfoChangedEventArgs e)
    {
    }
#endif
}
