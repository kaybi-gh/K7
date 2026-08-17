using AndroidX.Concurrent.Futures;
using AndroidX.Media3.Common;
using Google.Common.Util.Concurrent;

#pragma warning disable XAOBS001 // ResolvableFuture is the only way to create IListenableFuture in .NET Android bindings

namespace K7.Clients.MAUI.Platforms.Android.Services;

/// <summary>
/// Session-facing player that forwards to the active ExoPlayer and can swap that
/// instance in place after a crossfade (Media3 ForwardingSimpleBasePlayer.setPlayer).
/// Next/previous from Android Auto and notifications go through IAudioPlayerService
/// instead of Media3's native playlist.
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

    public K7ForwardingPlayer(
        IPlayer player,
        Func<bool> hasNext,
        Func<bool> hasPrevious,
        Action onSeekToNext,
        Action onSeekToPrevious) : base(player)
    {
        _hasNext = hasNext;
        _hasPrevious = hasPrevious;
        _onSeekToNext = onSeekToNext;
        _onSeekToPrevious = onSeekToPrevious;
    }

    public void SetActivePlayer(IPlayer player) => Player = player;

    public void NotifyQueueChanged() => InvalidateState();

    protected override State GetState()
    {
        var state = base.GetState()!;
        var commands = new PlayerCommands.Builder()
            .AddAll(state.AvailableCommands!)!;
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

        return state.BuildUpon()!.SetAvailableCommands(commands.Build()!)!.Build()!;
    }

    protected override IListenableFuture HandleSeek(int mediaItemIndex, long positionMs, int seekCommand)
    {
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

    // Service owns ExoPlayer lifetime; MediaSession.release must not release the audible player.
    protected override IListenableFuture HandleRelease() => ImmediateVoid();

    private static IListenableFuture ImmediateVoid()
    {
        var future = ResolvableFuture.Create()!;
        future.Set(null);
        return future;
    }
}
