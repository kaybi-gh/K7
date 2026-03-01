namespace K7.Shared.Interfaces;

/// <summary>
/// Client-side interface for the playback progress SignalR hub.
/// </summary>
public interface IPlaybackProgressClient
{
    Task ReceivePlaybackProgress(Guid mediaId, double progressPercentage, bool isCompleted);
}
