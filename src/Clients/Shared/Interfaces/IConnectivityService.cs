namespace K7.Clients.Shared.Interfaces;

public interface IConnectivityService
{
    bool IsOnline { get; }
    bool IsWifi { get; }
    bool IsCellular { get; }
    event Action<bool>? ConnectivityChanged;
}
