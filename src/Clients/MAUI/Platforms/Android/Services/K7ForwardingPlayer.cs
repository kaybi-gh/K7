using AndroidX.Concurrent.Futures;
using AndroidX.Media3.Common;
using Google.Common.Util.Concurrent;
using K7.Clients.Shared.Helpers;

#pragma warning disable XAOBS001 // ResolvableFuture is the only way to create IListenableFuture in .NET Android bindings

namespace K7.Clients.MAUI.Platforms.Android.Services;

/// <summary>
/// Session-facing player that forwards to the active ExoPlayer and can swap that
/// instance in place after a crossfade (Media3 ForwardingSimpleBasePlayer.setPlayer).
/// Next/previous from notifications, lock screen, and Bluetooth AVRCP go through
/// IAudioPlayerService when ExoPlayer has a single item. Multi-item Android Auto
/// playlists skip natively so artwork and audio stay aligned.
/// Shuffle and repeat always follow IAudioPlayerService, never ExoPlayer.
/// </summary>
public class K7ForwardingPlayer : ForwardingSimpleBasePlayer
{
    // Media3 command constants: SEEK_TO_PREVIOUS=7, SEEK_TO_PREVIOUS_MEDIA_ITEM=8,
    // SEEK_TO_NEXT=9, SEEK_TO_NEXT_MEDIA_ITEM=10
    private const int CommandSeekToPrevious = 7;
    private const int CommandSeekToPreviousMediaItem = 8;
    private const int CommandSeekToNext = 9;
    private const int CommandSeekToNextMediaItem = 10;

    private readonly Func<bool> _hasNext;
    private readonly Func<bool> _hasPrevious;
    private readonly Action _onSeekToNext;
    private readonly Action _onSeekToPrevious;
    private readonly Func<bool> _getShuffle;
    private readonly Action<bool> _setShuffle;
    private readonly Func<int> _getRepeat;
    private readonly Action<int> _setRepeat;

    public K7ForwardingPlayer(
        IPlayer player,
        Func<bool> hasNext,
        Func<bool> hasPrevious,
        Action onSeekToNext,
        Action onSeekToPrevious,
        Func<bool> getShuffle,
        Action<bool> setShuffle,
        Func<int> getRepeat,
        Action<int> setRepeat) : base(player)
    {
        _hasNext = hasNext;
        _hasPrevious = hasPrevious;
        _onSeekToNext = onSeekToNext;
        _onSeekToPrevious = onSeekToPrevious;
        _getShuffle = getShuffle;
        _setShuffle = setShuffle;
        _getRepeat = getRepeat;
        _setRepeat = setRepeat;
    }

    public void SetActivePlayer(IPlayer player) => Player = player;

    public void NotifyQueueChanged() => InvalidateState();

    protected override State GetState()
    {
        var state = base.GetState()!;
        var itemCount = Player?.MediaItemCount ?? 0;
        var playbackState = Media3SessionPlaybackState.ForSession(
            state.PlaybackState,
            itemCount,
            _hasNext is not null && _hasNext());

        var commands = new PlayerCommands.Builder()
            .AddAll(state.AvailableCommands!)!;
        commands.Add(AndroidAutoPlaybackCommands.CommandSetShuffleMode);
        commands.Add(AndroidAutoPlaybackCommands.CommandSetRepeatMode);

        if (itemCount > 0 && !HasNativePlaylist())
        {
            if (_hasPrevious is not null && _hasPrevious())
            {
                commands.Add(CommandSeekToPrevious);
                commands.Add(CommandSeekToPreviousMediaItem);
            }

            if (_hasNext is not null && _hasNext())
            {
                commands.Add(CommandSeekToNext);
                commands.Add(CommandSeekToNextMediaItem);
            }
        }
        else if (itemCount > 0 && ShouldRoutePreviousThroughQueue())
        {
            commands.Add(CommandSeekToPrevious);
            commands.Add(CommandSeekToPreviousMediaItem);
        }

        var builder = state.BuildUpon()!
            .SetAvailableCommands(commands.Build()!)!
            .SetShuffleModeEnabled(_getShuffle())!
            .SetRepeatMode(_getRepeat())!;
        if (playbackState != state.PlaybackState)
            builder.SetPlaybackState(playbackState);

        return builder.Build()!;
    }

    protected override IListenableFuture HandleSeek(int mediaItemIndex, long positionMs, int seekCommand)
    {
        // Tempus / Jellyfin / Media3 demo: when ExoPlayer already has a real
        // playlist, skip must seek that playlist. Intercepting next/prev and
        // routing through IAudioPlayerService used to patch now-playing metadata
        // (artwork/title change, URI stays) which is the AA / Bluetooth skip bug.
        if (HasNativePlaylist())
        {
            if (IsPreviousCommand(seekCommand) && ShouldRoutePreviousThroughQueue())
            {
                _onSeekToPrevious();
                return ImmediateVoid();
            }

            return base.HandleSeek(mediaItemIndex, positionMs, seekCommand)!;
        }

        if (seekCommand is CommandSeekToNext or CommandSeekToNextMediaItem)
        {
            _onSeekToNext();
            return ImmediateVoid();
        }

        if (seekCommand is CommandSeekToPrevious or CommandSeekToPreviousMediaItem)
        {
            _onSeekToPrevious();
            return ImmediateVoid();
        }

        return base.HandleSeek(mediaItemIndex, positionMs, seekCommand)!;
    }

    protected override IListenableFuture HandleSetShuffleModeEnabled(bool shuffleModeEnabled)
    {
        _setShuffle(shuffleModeEnabled);
        return ImmediateVoid();
    }

    protected override IListenableFuture HandleSetRepeatMode(int repeatMode)
    {
        _setRepeat(repeatMode);
        return ImmediateVoid();
    }

    private bool HasNativePlaylist() => Player?.MediaItemCount > 1;

    private bool ShouldRoutePreviousThroughQueue() =>
        AndroidAutoRadioPlaylist.ShouldRoutePreviousThroughQueue(
            Player?.CurrentMediaItemIndex ?? 0,
            _hasPrevious is not null && _hasPrevious());

    private static bool IsPreviousCommand(int seekCommand) =>
        seekCommand is CommandSeekToPrevious or CommandSeekToPreviousMediaItem;

    // Service owns ExoPlayer lifetime; MediaSession.release must not release the audible player.
    protected override IListenableFuture HandleRelease() => ImmediateVoid();

    private static IListenableFuture ImmediateVoid()
    {
        var future = ResolvableFuture.Create()!;
        future.Set(null);
        return future;
    }
}
