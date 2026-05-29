using K7.Clients.Shared.Interfaces;

namespace K7.Clients.MAUI.Services;

/// <summary>
/// No-op cast service for platforms that do not support Google Cast (iOS, Windows, macOS).
/// </summary>
internal sealed class NullCastService : ICastService
{
    public bool IsAvailable => false;
    public bool IsCasting => false;
    public IReadOnlyList<CastDeviceInfo> DiscoveredDevices => [];

#pragma warning disable CS0067
    public event Action? StateChanged;
    public event Action<IReadOnlyList<CastDeviceInfo>>? DevicesDiscovered;
    public event Action<CastMediaStatus>? MediaStatusUpdated;
#pragma warning restore CS0067

    public Task StartDiscoveryAsync() => Task.CompletedTask;
    public Task StopDiscoveryAsync() => Task.CompletedTask;
    public Task CastAsync(CastMediaRequest request) => Task.CompletedTask;
    public Task StopCastingAsync() => Task.CompletedTask;
    public Task SendTransportCommandAsync(CastTransportCommand command) => Task.CompletedTask;
}
