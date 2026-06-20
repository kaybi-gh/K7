using System.Collections.Concurrent;

namespace K7.Server.Application.Services;

public sealed record HubDeviceConnection(
    string ConnectionId,
    string IdentityUserId,
    string UserDisplayName,
    string DeviceName,
    string DeviceType,
    bool SyncPlayEnabled = true);

public interface IHubPresenceTracker
{
    void RegisterDevice(Guid deviceId, HubDeviceConnection connection);
    bool TryRemoveByConnectionId(string connectionId, out Guid deviceId, out HubDeviceConnection? connection);
    bool TryGetDevice(Guid deviceId, out HubDeviceConnection connection);
    void UpdateDevice(Guid deviceId, HubDeviceConnection connection);
    IEnumerable<KeyValuePair<Guid, HubDeviceConnection>> GetDevicesForUser(string identityUserId);
    IEnumerable<KeyValuePair<Guid, HubDeviceConnection>> GetAllDevices();
    int GetOnlineUserCount();
    int GetConnectedDeviceCount();
    IReadOnlyList<string> GetOnlineIdentityUserIds();
    (Guid DeviceId, HubDeviceConnection Connection)? FindByConnectionId(string connectionId);
}

public sealed class HubPresenceTracker : IHubPresenceTracker
{
    private readonly ConcurrentDictionary<Guid, HubDeviceConnection> _connectedDevices = new();

    public void RegisterDevice(Guid deviceId, HubDeviceConnection connection) =>
        _connectedDevices[deviceId] = connection;

    public bool TryRemoveByConnectionId(string connectionId, out Guid deviceId, out HubDeviceConnection? connection)
    {
        connection = null;
        deviceId = default;

        var entry = _connectedDevices.FirstOrDefault(kvp => kvp.Value.ConnectionId == connectionId);
        if (entry.Value is null)
            return false;

        deviceId = entry.Key;
        connection = entry.Value;
        return _connectedDevices.TryRemove(deviceId, out _);
    }

    public bool TryGetDevice(Guid deviceId, out HubDeviceConnection connection) =>
        _connectedDevices.TryGetValue(deviceId, out connection!);

    public void UpdateDevice(Guid deviceId, HubDeviceConnection connection) =>
        _connectedDevices[deviceId] = connection;

    public IEnumerable<KeyValuePair<Guid, HubDeviceConnection>> GetDevicesForUser(string identityUserId) =>
        _connectedDevices.Where(kvp => kvp.Value.IdentityUserId == identityUserId);

    public IEnumerable<KeyValuePair<Guid, HubDeviceConnection>> GetAllDevices() =>
        _connectedDevices;

    public int GetOnlineUserCount() =>
        _connectedDevices.Values.Select(d => d.IdentityUserId).Distinct().Count();

    public int GetConnectedDeviceCount() =>
        _connectedDevices.Count;

    public IReadOnlyList<string> GetOnlineIdentityUserIds() =>
        _connectedDevices.Values
            .Select(d => d.IdentityUserId)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    public (Guid DeviceId, HubDeviceConnection Connection)? FindByConnectionId(string connectionId)
    {
        var entry = _connectedDevices.FirstOrDefault(kvp => kvp.Value.ConnectionId == connectionId);
        if (entry.Value is null)
            return null;

        return (entry.Key, entry.Value);
    }
}
