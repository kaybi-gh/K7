using System.Text.Json;
using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Dtos.SharedProfiles;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public class SharedProfileLocalCache(
    IDeviceStorageService storage,
    ISharedProfileApi api,
    IConnectivityService connectivity) : ISharedProfileLocalCache
{
    public IReadOnlyList<SharedProfileDto> GetCached()
    {
        var json = storage.Get(PreferenceKeys.SHARED_PROFILES_CACHE);
        if (string.IsNullOrEmpty(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<SharedProfileDto>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public SharedProfileDto? FindById(Guid id) =>
        GetCached().FirstOrDefault(g => g.Id == id);

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!connectivity.IsOnline)
            return;

        try
        {
            var groups = await api.GetSharedProfilesAsync(cancellationToken);
            UpdateCache(groups);
        }
        catch
        {
            // Keep existing cache on failure
        }
    }

    public void UpdateCache(IReadOnlyList<SharedProfileDto> groups)
    {
        var json = JsonSerializer.Serialize(groups);
        storage.Set(PreferenceKeys.SHARED_PROFILES_CACHE, json);
    }
}
