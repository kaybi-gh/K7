using K7.Shared.Dtos.SharedProfiles;

namespace K7.Clients.Shared.Interfaces;

public interface ISharedProfileLocalCache
{
    IReadOnlyList<SharedProfileDto> GetCached();
    SharedProfileDto? FindById(Guid id);
    Task RefreshAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts <paramref name="groups"/> into the device-wide cache by id.
    /// Groups already cached but missing from the list are kept so a pinned
    /// shared profile stays on the profile picker after another local user signs in.
    /// </summary>
    void UpdateCache(IReadOnlyList<SharedProfileDto> groups);
}
