namespace K7.Shared.Interfaces;

/// <summary>
/// Client-side interface for user home layout SignalR updates.
/// </summary>
public interface IUserHomeLayoutClient
{
    /// <summary>
    /// The home layout of the connected user changed on one of their devices: the home is to be
    /// loaded again.
    /// </summary>
    Task ReceiveHomeLayoutUpdated();
}
