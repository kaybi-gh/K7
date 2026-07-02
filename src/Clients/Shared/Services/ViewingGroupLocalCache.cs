using System.Text.Json;
using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Dtos.ViewingGroups;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public class ViewingGroupLocalCache(
    IDeviceStorageService storage,
    IViewingGroupApi api,
    IConnectivityService connectivity) : IViewingGroupLocalCache
{
    public IReadOnlyList<ViewingGroupDto> GetCached()
    {
        var json = storage.Get(PreferenceKeys.VIEWING_GROUPS_CACHE);
        if (string.IsNullOrEmpty(json))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<ViewingGroupDto>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    public ViewingGroupDto? FindById(Guid id) =>
        GetCached().FirstOrDefault(g => g.Id == id);

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (!connectivity.IsOnline)
            return;

        try
        {
            var groups = await api.GetViewingGroupsAsync(cancellationToken);
            UpdateCache(groups);
        }
        catch
        {
            // Keep existing cache on failure
        }
    }

    public void UpdateCache(IReadOnlyList<ViewingGroupDto> groups)
    {
        var json = JsonSerializer.Serialize(groups);
        storage.Set(PreferenceKeys.VIEWING_GROUPS_CACHE, json);
    }
}
