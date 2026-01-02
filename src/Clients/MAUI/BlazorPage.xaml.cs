using CommunityToolkit.Maui.Core;
using CommunityToolkit.Maui.Views;
using K7.Clients.Shared.Domain.Interfaces;

namespace K7.Clients.MAUI;

public partial class BlazorPage : ContentPage
{
    private readonly IPlayerService _playerService;

    public BlazorPage(IPlayerService playerService)
    {
        _playerService = playerService;
        InitializeComponent();
        InitializePlayer();
//        BlazorWebViewHandler.BlazorWebViewMapper.AppendToMapping("MyBlazorCustomization", (handler, view) => {
//#if IOS || MACCATALYST
//        handler.PlatformView.Opaque = false;
//        handler.PlatformView.BackgroundColor = UIKit.UIColor.Clear;
//#elif WINDOWS
//        handler.PlatformView.Opacity = 1;
//        handler.PlatformView.DefaultBackgroundColor = new Windows.UI.Color() { A = 0, R = 0, G = 0, B = 0 };
//#elif ANDROID
//        handler.PlatformView.SetBackgroundColor(Android.Graphics.Color.Argb(alpha: 0, red: 0, green: 0, blue: 0));
//#endif
//        });
    }

    private void InitializePlayer()
    {
        NativePlayer.Volume = _playerService.Volume;
        NativePlayer.ShouldMute = _playerService.IsMuted;
        NativePlayer.MediaOpened += NativePlayer_MediaOpened;
        NativePlayer.MediaEnded += NativePlayer_MediaEnded;
        NativePlayer.MediaFailed += NativePlayer_MediaFailed;
        NativePlayer.PositionChanged += NativePlayer_PositionChanged;
        _playerService.PlayRequested += () => { NativePlayer.Play(); return Task.CompletedTask; };
        _playerService.PauseRequested += () => { NativePlayer.Pause(); return Task.CompletedTask; };
        _playerService.MuteRequested += () => { NativePlayer.ShouldMute = true; return Task.CompletedTask; };
        _playerService.UnmuteRequest += () => { NativePlayer.ShouldMute = false; return Task.CompletedTask; };
        _playerService.VolumeChangeRequested += (volume) => { NativePlayer.Volume = volume; return Task.CompletedTask; };
        _playerService.PlaybackRateChangeRequested += (rate) => { NativePlayer.Speed = rate; return Task.CompletedTask; };
        _playerService.StopRequested += () => { NativePlayer.Stop(); return Task.CompletedTask; };
        //_playerService.EnterFullScreenRequested += () => { NativePlayer.full(); return Task.CompletedTask; };
        //_playerService.ExitFullScreenRequested += ExitFullScreenAsync;
        _playerService.SeekRequested += (position) => { NativePlayer.SeekTo(TimeSpan.FromSeconds(position)); return Task.CompletedTask; };
        _playerService.Duration = NativePlayer.Duration.TotalSeconds;
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
                switch (NativePlayer.CurrentState)
                {
                    case MediaElementState.Buffering:
                        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Buffering;
                        break;

                    case MediaElementState.Failed:
                        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Unknown;
                        break;

                    case MediaElementState.None:
                        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Unknown;
                        break;

                    case MediaElementState.Opening:
                        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Idle;
                        break;

                    case MediaElementState.Paused:
                        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Paused;
                        break;

                    case MediaElementState.Playing:
                        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Playing;
                        break;

                    case MediaElementState.Stopped:
                        _playerService.PlaybackState = Server.Domain.Enums.PlaybackState.Idle;
                        break;
                };
            }
        };
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
        
    }

    private void NativePlayer_PositionChanged(object? sender, MediaPositionChangedEventArgs e)
    {
        _playerService.CurrentTime = e.Position.TotalSeconds;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        DisposeMediaElement();
    }

    private void DisposeMediaElement()
    {
        if (NativePlayer != null)
        {
            NativePlayer.Stop();
        }
    }
}
