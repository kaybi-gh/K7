using Foundation;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Interfaces;
using MediaPlayer;

namespace K7.Clients.MAUI.Platforms.iOS.Services;

/// <summary>
/// Integrates with MPNowPlayingInfoCenter and MPRemoteCommandCenter
/// to provide lock screen, Control Center, and CarPlay now-playing controls.
/// </summary>
public class NowPlayingService
{
    private readonly IAudioPlayerService _audioPlayerService;
    private readonly IK7ServerService _k7ServerService;
    private MPMediaItemArtwork? _currentArtwork;

    public NowPlayingService(IAudioPlayerService audioPlayerService, IK7ServerService k7ServerService)
    {
        _audioPlayerService = audioPlayerService;
        _k7ServerService = k7ServerService;
        ConfigureRemoteCommands();
        SubscribeToEvents();
    }

    private void ConfigureRemoteCommands()
    {
        var commandCenter = MPRemoteCommandCenter.Shared;

        commandCenter.PlayCommand.Enabled = true;
        commandCenter.PlayCommand.AddTarget(OnPlayCommand);

        commandCenter.PauseCommand.Enabled = true;
        commandCenter.PauseCommand.AddTarget(OnPauseCommand);

        commandCenter.NextTrackCommand.Enabled = true;
        commandCenter.NextTrackCommand.AddTarget(OnNextCommand);

        commandCenter.PreviousTrackCommand.Enabled = true;
        commandCenter.PreviousTrackCommand.AddTarget(OnPreviousCommand);

        commandCenter.ChangePlaybackPositionCommand.Enabled = true;
        commandCenter.ChangePlaybackPositionCommand.AddTarget(OnSeekCommand);

        commandCenter.TogglePlayPauseCommand.Enabled = true;
        commandCenter.TogglePlayPauseCommand.AddTarget(OnTogglePlayPauseCommand);
    }

    private void SubscribeToEvents()
    {
        _audioPlayerService.CurrentTrackChanged += OnCurrentTrackChanged;
        _audioPlayerService.PlaybackStateChanged += OnPlaybackStateChanged;
        _audioPlayerService.DurationChanged += OnDurationChanged;
    }

    private void OnCurrentTrackChanged(AudioQueueItem? track)
    {
        if (track is null)
        {
            MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = new MPNowPlayingInfo();
            return;
        }

        UpdateNowPlayingInfo(track);

        if (track.CoverUrl is not null)
            _ = LoadArtworkAsync(track.CoverUrl);
    }

    private void UpdateNowPlayingInfo(AudioQueueItem? track = null)
    {
        var info = new MPNowPlayingInfo
        {
            Title = track?.Title ?? _audioPlayerService.CurrentTrack?.Title ?? "",
            Artist = track?.Artist ?? _audioPlayerService.CurrentTrack?.Artist ?? "",
            AlbumTitle = track?.AlbumTitle ?? _audioPlayerService.CurrentTrack?.AlbumTitle ?? "",
            PlaybackDuration = _audioPlayerService.Duration,
            ElapsedPlaybackTime = _audioPlayerService.CurrentTime,
            PlaybackRate = _audioPlayerService.PlaybackState == PlaybackState.Playing ? 1.0 : 0.0,
        };

        if (_currentArtwork is not null)
            info.Artwork = _currentArtwork;

        MPNowPlayingInfoCenter.DefaultCenter.NowPlaying = info;
    }

    private void OnPlaybackStateChanged(PlaybackState state)
    {
        UpdateNowPlayingInfo();
    }

    private void OnDurationChanged(double duration)
    {
        UpdateNowPlayingInfo();
    }

    private async Task LoadArtworkAsync(string coverUrl)
    {
        try
        {
            var absoluteUri = _k7ServerService.GetAbsoluteUri(coverUrl);
            if (absoluteUri is null) return;

            using var client = new HttpClient();
            var data = await client.GetByteArrayAsync(absoluteUri);
            var nsData = NSData.FromArray(data);
            var image = UIKit.UIImage.LoadFromData(nsData);

            if (image is null) return;

            _currentArtwork = new MPMediaItemArtwork(image.Size, _ => image);

            MainThread.BeginInvokeOnMainThread(() => UpdateNowPlayingInfo());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[K7-iOS-NowPlaying] Artwork load failed: {ex.Message}");
        }
    }

    private MPRemoteCommandHandlerStatus OnPlayCommand(MPRemoteCommandEvent commandEvent)
    {
        _audioPlayerService.Play();
        return MPRemoteCommandHandlerStatus.Success;
    }

    private MPRemoteCommandHandlerStatus OnPauseCommand(MPRemoteCommandEvent commandEvent)
    {
        _audioPlayerService.Pause();
        return MPRemoteCommandHandlerStatus.Success;
    }

    private MPRemoteCommandHandlerStatus OnNextCommand(MPRemoteCommandEvent commandEvent)
    {
        _ = _audioPlayerService.NextAsync();
        return MPRemoteCommandHandlerStatus.Success;
    }

    private MPRemoteCommandHandlerStatus OnPreviousCommand(MPRemoteCommandEvent commandEvent)
    {
        _ = _audioPlayerService.PreviousAsync();
        return MPRemoteCommandHandlerStatus.Success;
    }

    private MPRemoteCommandHandlerStatus OnSeekCommand(MPRemoteCommandEvent commandEvent)
    {
        if (commandEvent is MPChangePlaybackPositionCommandEvent positionEvent)
        {
            _audioPlayerService.Seek(positionEvent.PositionTime);
            return MPRemoteCommandHandlerStatus.Success;
        }

        return MPRemoteCommandHandlerStatus.CommandFailed;
    }

    private MPRemoteCommandHandlerStatus OnTogglePlayPauseCommand(MPRemoteCommandEvent commandEvent)
    {
        if (_audioPlayerService.PlaybackState == PlaybackState.Playing)
            _audioPlayerService.Pause();
        else
            _audioPlayerService.Play();
        return MPRemoteCommandHandlerStatus.Success;
    }
}
