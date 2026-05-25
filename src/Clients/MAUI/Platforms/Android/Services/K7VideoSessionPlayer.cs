using Android.OS;
using AndroidX.Concurrent.Futures;
using AndroidX.Media3.Common;
using Google.Common.Util.Concurrent;
using K7.Clients.Shared.Interfaces;

#pragma warning disable XAOBS001

namespace K7.Clients.MAUI.Platforms.Android.Services;

/// <summary>
/// A virtual player (SimpleBasePlayer) that reports video playback state to the MediaSession
/// without actually playing any media. This drives the notification for video playback,
/// following the same pattern as Jellyfin's MediaSessionPlayer.
/// Commands (play/pause/seek) are forwarded to IPlayerService.
/// </summary>
public class K7VideoSessionPlayer : SimpleBasePlayer
{
    private readonly IPlayerService _playerService;
    private readonly Looper _looper;

    private bool _isActive;
    private string _title = "Video";
    private global::Android.Net.Uri? _artworkUri;

    public K7VideoSessionPlayer(Looper looper, IPlayerService playerService) : base(looper)
    {
        _looper = looper;
        _playerService = playerService;
    }

    public void Activate(string? title, string? coverUrl)
    {
        _isActive = true;
        _title = title ?? "Video";
        _artworkUri = coverUrl is not null ? global::Android.Net.Uri.Parse(coverUrl) : null;
        new Handler(_looper).Post(InvalidateState);
    }

    public void UpdateMetadata(string? title, string? coverUrl)
    {
        _title = title ?? "Video";
        _artworkUri = coverUrl is not null ? global::Android.Net.Uri.Parse(coverUrl) : null;
        new Handler(_looper).Post(InvalidateState);
    }

    public void NotifyStateChanged()
    {
        new Handler(_looper).Post(InvalidateState);
    }

    public void Deactivate()
    {
        _isActive = false;
        new Handler(_looper).Post(InvalidateState);
    }

    protected override State GetState()
    {
        var stateBuilder = new State.Builder();

        if (!_isActive)
        {
            stateBuilder.SetPlaylist(new List<MediaItemData>());
            stateBuilder.SetPlaybackState(1); // STATE_IDLE
            stateBuilder.SetPlayWhenReady(false, 1); // PLAY_WHEN_READY_CHANGE_REASON_USER_REQUEST
            return stateBuilder.Build()!;
        }

        // Available commands (raw int values from IPlayer)
        var commands = new PlayerCommands.Builder()
            .Add(1)!  // COMMAND_PLAY_PAUSE
            .Add(2)!  // COMMAND_PREPARE
            .Add(3)!  // COMMAND_STOP
            .Add(6)!  // COMMAND_SEEK_IN_CURRENT_MEDIA_ITEM
            .Add(16)! // COMMAND_GET_CURRENT_MEDIA_ITEM
            .Add(17)! // COMMAND_GET_TIMELINE
            .Add(18)! // COMMAND_GET_MEDIA_ITEMS_METADATA
            .Build()!;
        stateBuilder.SetAvailableCommands(commands);

        // Media item with video metadata
        var metadata = new MediaMetadata.Builder()
            .SetTitle(_title)!
            .SetIsPlayable(Java.Lang.Boolean.ValueOf(true))!
            .SetMediaType(Java.Lang.Integer.ValueOf((int)MediaMetadata.MediaTypeMovie))!;

        if (_artworkUri is not null)
            metadata.SetArtworkUri(_artworkUri);

        var durationUs = _playerService.Duration > 0
            ? (long)(_playerService.Duration * 1_000_000)
            : -9223372036854775807L; // C.TIME_UNSET

        var playlistItem = new MediaItemData.Builder("video_session")
            .SetMediaMetadata(metadata.Build()!)!
            .SetDurationUs(durationUs)!
            .Build()!;

        stateBuilder.SetPlaylist([playlistItem]);
        stateBuilder.SetCurrentMediaItemIndex(0);

        // Playback state
        var isPlaying = _playerService.PlaybackState == K7.Server.Domain.Enums.PlaybackState.Playing;
        var isBuffering = _playerService.PlaybackState == K7.Server.Domain.Enums.PlaybackState.Buffering;

        stateBuilder.SetPlayWhenReady(isPlaying || isBuffering, 1); // USER_REQUEST
        stateBuilder.SetPlaybackState(isBuffering ? 2 : 3); // STATE_BUFFERING : STATE_READY

        // Position
        stateBuilder.SetContentPositionMs((long)(_playerService.CurrentTime * 1000));

        return stateBuilder.Build()!;
    }

    protected override IListenableFuture HandleSetPlayWhenReady(bool playWhenReady)
    {
        if (playWhenReady)
            _playerService.Play();
        else
            _playerService.Pause();
        var future = ResolvableFuture.Create()!;
        future.Set(null);
        return future;
    }

    protected override IListenableFuture HandleSeek(
        int mediaItemIndex,
        long positionMs,
        int seekCommand)
    {
        _playerService.Seek(positionMs / 1000.0);
        var future = ResolvableFuture.Create()!;
        future.Set(null);
        return future;
    }

    protected override IListenableFuture HandleStop()
    {
        _playerService.Stop();
        var future = ResolvableFuture.Create()!;
        future.Set(null);
        return future;
    }
}
