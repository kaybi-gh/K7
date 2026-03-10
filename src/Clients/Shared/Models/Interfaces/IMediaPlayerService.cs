namespace K7.Clients.Shared.Domain.Interfaces;

/// <summary>
/// Coordinates audio and video players: ensures only one is active at a time.
/// </summary>
public interface IMediaPlayerService : IDisposable
{
    ActivePlayerType ActivePlayer { get; }
    event Action<ActivePlayerType>? ActivePlayerChanged;
}

public enum ActivePlayerType
{
    None,
    Video,
    Audio
}
