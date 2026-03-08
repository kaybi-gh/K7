using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Domain.Models;

namespace K7.Clients.MAUI;

public partial class BlazorPage : ContentPage
{
    private readonly IPlayerService _playerService;

    public BlazorPage(IPlayerService playerService)
    {
        _playerService = playerService;
        InitializeComponent();
        InitializePlayer();
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
                NativePlayer.Source = MediaSource.FromUri(source.Url);
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
#if WINDOWS
                // WebView2 transparency is broken on WinUI3 (microsoft-ui-xaml#6527).
                // Place MediaElement in front of the BlazorWebView and use native controls.
                NativePlayer.ZIndex = 3;
                NativePlayer.ShouldShowPlaybackControls = true;
                NativePlayer.InputTransparent = false;
#endif
            }
            else
            {
                NativePlayer.Stop();
                NativePlayer.Source = null;
#if WINDOWS
                NativePlayer.ZIndex = 1;
                NativePlayer.ShouldShowPlaybackControls = false;
                NativePlayer.InputTransparent = true;
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
    }
}
