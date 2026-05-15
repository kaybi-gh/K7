#if WINDOWS
using K7.Clients.Shared.Models;
using Windows.Media;
using Windows.Media.Playback;
using Windows.Storage.Streams;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace K7.Clients.MAUI;

public partial class BlazorPage
{
    private SystemMediaTransportControls? _smtc;

    partial void InitializePlayerPlatform()
    {
        _audioPlayerService.CurrentTrackChanged += OnAudioTrackChangedWindows;
        _audioPlayerService.PlaybackStateChanged += OnAudioPlaybackStateChangedWindows;

        NativeAudioPlayer.HandlerChanged += async (_, _) =>
        {
            if (!TrySetupSmtc())
            {
                await Task.Delay(500);
                TrySetupSmtc();
            }
        };
    }

    private bool TrySetupSmtc()
    {
        if (_smtc is not null) return true;

        var platformView = NativeAudioPlayer.Handler?.PlatformView;
        if (platformView is not Panel panel)
        {
            System.Diagnostics.Trace.WriteLine($"[K7-Audio] SMTC: platform view is {platformView?.GetType().Name ?? "null"}, expected Panel");
            return false;
        }

        MediaPlayerElement? mpe = null;
        foreach (var child in panel.Children)
        {
            if (child is MediaPlayerElement found)
            {
                mpe = found;
                break;
            }
        }

        if (mpe?.MediaPlayer is null)
        {
            System.Diagnostics.Trace.WriteLine($"[K7-Audio] SMTC: no MediaPlayerElement in panel ({panel.Children.Count} children)");
            return false;
        }

        mpe.MediaPlayer.CommandManager.IsEnabled = false;

        var smtc = mpe.MediaPlayer.SystemMediaTransportControls;
        smtc.IsEnabled = true;
        smtc.IsPlayEnabled = true;
        smtc.IsPauseEnabled = true;
        smtc.IsNextEnabled = true;
        smtc.IsPreviousEnabled = true;
        smtc.ButtonPressed += OnSmtcButtonPressed;
        _smtc = smtc;

        System.Diagnostics.Trace.WriteLine("[K7-Audio] SMTC setup OK");
        return true;
    }

    private void OnSmtcButtonPressed(SystemMediaTransportControls sender, SystemMediaTransportControlsButtonPressedEventArgs args)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            switch (args.Button)
            {
                case SystemMediaTransportControlsButton.Play:
                    _audioPlayerService.Play();
                    break;
                case SystemMediaTransportControlsButton.Pause:
                    _audioPlayerService.Pause();
                    break;
                case SystemMediaTransportControlsButton.Next:
                    _ = _audioPlayerService.NextAsync();
                    break;
                case SystemMediaTransportControlsButton.Previous:
                    _ = _audioPlayerService.PreviousAsync();
                    break;
            }
        });
    }

    private void OnAudioPlaybackStateChangedWindows(Server.Domain.Enums.PlaybackState state)
    {
        if (_smtc is null) return;

        _smtc.PlaybackStatus = state switch
        {
            Server.Domain.Enums.PlaybackState.Playing => MediaPlaybackStatus.Playing,
            Server.Domain.Enums.PlaybackState.Paused => MediaPlaybackStatus.Paused,
            Server.Domain.Enums.PlaybackState.Buffering => MediaPlaybackStatus.Changing,
            _ => MediaPlaybackStatus.Stopped,
        };
    }

    private void OnAudioTrackChangedWindows(AudioQueueItem? track)
    {
        if (track is null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            TrySetupSmtc();
            if (_smtc is null) return;

            try
            {
                var updater = _smtc.DisplayUpdater;
                updater.Type = MediaPlaybackType.Music;
                updater.MusicProperties.Title = track.Title ?? "";
                updater.MusicProperties.Artist = track.Artist ?? "";
                updater.MusicProperties.AlbumTitle = track.AlbumTitle ?? "";

                if (!string.IsNullOrEmpty(track.CoverUrl))
                {
                    var absoluteUri = _k7ServerService.GetAbsoluteUri(track.CoverUrl);
                    if (absoluteUri is not null)
                    {
                        updater.Thumbnail = RandomAccessStreamReference.CreateFromUri(absoluteUri);
                    }
                }

                updater.Update();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[K7-Audio] SMTC update failed: {ex.Message}");
            }
        });
    }
}
#endif
