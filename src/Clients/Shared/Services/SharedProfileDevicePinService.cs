using System.Text.Json;
using K7.Clients.Shared.Interfaces;
using K7.Shared;

namespace K7.Clients.Shared.Services;

public class SharedProfileDevicePinService(IDeviceStorageService storage) : ISharedProfileDevicePinService
{
    public IReadOnlySet<Guid> GetPinnedGroupIds()
    {
        var json = storage.Get(PreferenceKeys.PINNED_SHARED_PROFILE_IDS);
        if (string.IsNullOrEmpty(json))
            return new HashSet<Guid>();

        try
        {
            return JsonSerializer.Deserialize<HashSet<Guid>>(json) ?? new HashSet<Guid>();
        }
        catch
        {
            return new HashSet<Guid>();
        }
    }

    public bool IsPinned(Guid groupId) => GetPinnedGroupIds().Contains(groupId);

    public void SetPinned(Guid groupId, bool pinned)
    {
        var pinnedIds = GetPinnedGroupIds().ToHashSet();

        if (pinned)
            pinnedIds.Add(groupId);
        else
            pinnedIds.Remove(groupId);

        var json = JsonSerializer.Serialize(pinnedIds);
        storage.Set(PreferenceKeys.PINNED_SHARED_PROFILE_IDS, json);
    }
}
