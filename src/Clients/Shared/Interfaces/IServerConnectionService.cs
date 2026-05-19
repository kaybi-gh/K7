namespace K7.Clients.Shared.Interfaces;

public interface IServerConnectionService
{
    /// <summary>
    /// Disconnects from the current server, clears local data,
    /// and navigates back to the server setup screen.
    /// </summary>
    void DisconnectAndReset();
}
