using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.SignalR.Client;

namespace K7.Clients.Shared.Services;

public sealed class NowPlayingService : IDisposable
{
    private readonly K7HubClient _hub;
    private readonly IDeviceStorageService _deviceStorage;

    public NowPlayingService(K7HubClient hub, IDeviceStorageService deviceStorage)
    {
        _hub = hub;
        _deviceStorage = deviceStorage;
        _hub.NowPlayingUpdated += OnNowPlayingUpdated;
        _hub.ConnectionStateChanged += OnConnectionStateChanged;
    }

    public IReadOnlyList<NowPlayingSessionDto> OtherDeviceSessions { get; private set; } = [];

    public event Action? Changed;

    public Task RefreshAsync() => _hub.RequestNowPlayingAsync();

    internal static IReadOnlyList<NowPlayingSessionDto> FilterOtherDevices(
        IReadOnlyList<NowPlayingSessionDto> sessions,
        Guid? selfDeviceId)
    {
        if (sessions.Count == 0)
            return [];

        if (selfDeviceId is null)
            return sessions;

        return sessions
            .Where(s => s.DeviceId is not Guid id || id != selfDeviceId)
            .ToList();
    }

    private void OnConnectionStateChanged(HubConnectionState state)
    {
        if (state == HubConnectionState.Connected)
            _ = RefreshAsync();
    }

    private void OnNowPlayingUpdated(IReadOnlyList<NowPlayingSessionDto> sessions)
    {
        var currentDeviceId = _deviceStorage.Get(PreferenceKeys.DEVICE_ID);
        Guid.TryParse(currentDeviceId, out var selfId);

        OtherDeviceSessions = FilterOtherDevices(
            sessions,
            selfId == Guid.Empty ? null : selfId);
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _hub.NowPlayingUpdated -= OnNowPlayingUpdated;
        _hub.ConnectionStateChanged -= OnConnectionStateChanged;
    }
}
