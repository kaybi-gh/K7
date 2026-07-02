using K7.Shared.Dtos.ViewingGroups;

namespace K7.Clients.Shared.Interfaces;

public interface IViewingGroupLocalCache
{
    IReadOnlyList<ViewingGroupDto> GetCached();
    ViewingGroupDto? FindById(Guid id);
    Task RefreshAsync(CancellationToken cancellationToken = default);
    void UpdateCache(IReadOnlyList<ViewingGroupDto> groups);
}
