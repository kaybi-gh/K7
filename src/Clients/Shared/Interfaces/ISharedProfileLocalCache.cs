using K7.Shared.Dtos.SharedProfiles;

namespace K7.Clients.Shared.Interfaces;

public interface ISharedProfileLocalCache
{
    IReadOnlyList<SharedProfileDto> GetCached();
    SharedProfileDto? FindById(Guid id);
    Task RefreshAsync(CancellationToken cancellationToken = default);
    void UpdateCache(IReadOnlyList<SharedProfileDto> groups);
}
