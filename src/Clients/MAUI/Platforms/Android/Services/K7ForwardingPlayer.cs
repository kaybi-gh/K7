using AndroidX.Media3.Common;

namespace K7.Clients.MAUI.Platforms.Android.Services;

/// <summary>
/// Wraps an ExoPlayer instance to intercept next/previous commands from the MediaSession.
/// This allows Android Auto and notification controls to trigger queue navigation
/// through IAudioPlayerService instead of Media3's native playlist.
/// </summary>
public class K7ForwardingPlayer : ForwardingPlayer
{
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

    public override bool HasNextMediaItem => _hasNext();

    public override bool HasPreviousMediaItem => _hasPrevious();

    public override PlayerCommands AvailableCommands
    {
        get
        {
            // Media3 command constants: SEEK_TO_PREVIOUS=7, SEEK_TO_PREVIOUS_MEDIA_ITEM=8,
            // SEEK_TO_NEXT=9, SEEK_TO_NEXT_MEDIA_ITEM=10
            var commands = base.AvailableCommands;
            return new PlayerCommands.Builder()
                .AddAll(commands)!
                .Add(7)!  // COMMAND_SEEK_TO_PREVIOUS
                .Add(8)!  // COMMAND_SEEK_TO_PREVIOUS_MEDIA_ITEM
                .Add(9)!  // COMMAND_SEEK_TO_NEXT
                .Add(10)! // COMMAND_SEEK_TO_NEXT_MEDIA_ITEM
                .Build()!;
        }
    }

    public override void SeekToNextMediaItem()
    {
        _onSeekToNext();
    }

    public override void SeekToPreviousMediaItem()
    {
        _onSeekToPrevious();
    }

    public override void SeekToNext()
    {
        _onSeekToNext();
    }

    public override void SeekToPrevious()
    {
        _onSeekToPrevious();
    }
}
