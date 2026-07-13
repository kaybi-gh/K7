using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface IExploreGroupStore
{
    event Action<Guid>? Changed;

    Task<ExploreGroupSnapshot?> EnsureGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    void Invalidate(Guid groupId);
}
