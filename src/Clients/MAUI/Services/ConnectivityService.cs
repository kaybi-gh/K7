using K7.Clients.Shared.Interfaces;

namespace K7.Clients.MAUI.Services;

public class ConnectivityService : IConnectivityService
{
    public bool IsOnline => Connectivity.Current.NetworkAccess == NetworkAccess.Internet;

    public bool IsWifi => Connectivity.Current.ConnectionProfiles.Contains(ConnectionProfile.WiFi);

    public bool IsCellular => Connectivity.Current.ConnectionProfiles.Contains(ConnectionProfile.Cellular);

    public event Action<bool>? ConnectivityChanged;

    public ConnectivityService()
    {
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e)
    {
        var isOnline = e.NetworkAccess == NetworkAccess.Internet;
        ConnectivityChanged?.Invoke(isOnline);
    }
}
