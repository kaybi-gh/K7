using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface IWindowsMpcPlaybackHost
{
    bool IsActive { get; }

    Task<bool> TryPlayAsync(
        WindowsMpcPlayRequest request,
        IPlayerService player,
        CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
