using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface ICastOrchestrationService
{
    Task CastCurrentVideoAsync(CastDeviceInfo device, CancellationToken cancellationToken = default);
    Task CastCurrentAudioAsync(CastDeviceInfo device, CancellationToken cancellationToken = default);
    Task StopCastingAsync(CancellationToken cancellationToken = default);
}
