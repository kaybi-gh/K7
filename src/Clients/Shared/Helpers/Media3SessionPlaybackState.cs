namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Media3 <c>Player</c> playback states. READY or BUFFERING with an empty
/// timeline throws <c>Empty playlist only allowed in STATE_IDLE or STATE_ENDED</c>.
/// </summary>
public static class Media3SessionPlaybackState
{
    public const int Idle = 1;
    public const int Buffering = 2;
    public const int Ready = 3;
    public const int Ended = 4;

    /// <summary>
    /// Android Auto radio fills the in-memory queue before ExoPlayer has items.
    /// Do not advertise READY on an empty timeline (lock-screen Next overlay).
    /// </summary>
    public static int ForSession(int playbackState, int mediaItemCount, bool hasNext)
    {
        if (mediaItemCount <= 0)
            return playbackState is Buffering or Ready ? Idle : playbackState;

        if (playbackState == Ended && hasNext)
            return Ready;

        return playbackState;
    }
}
