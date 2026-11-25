using System.Text.Json;
using K7.Clients.Shared.Interfaces;
using K7.Shared;

namespace K7.Clients.Shared.Helpers;

public static class ProfilePickerHiddenUsers
{
    public static HashSet<string> Read(IDeviceStorageService storage)
    {
        var json = storage.Get(PreferenceKeys.HIDDEN_PROFILE_PICKER_USER_IDS);
        if (string.IsNullOrEmpty(json))
            return new HashSet<string>(StringComparer.Ordinal);

        try
        {
            var ids = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            return ids
                .Where(id => !string.IsNullOrEmpty(id))
                .ToHashSet(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }
    }

    public static void Write(IDeviceStorageService storage, IEnumerable<string> identityUserIds)
    {
        var ids = identityUserIds
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (ids.Count == 0)
        {
            storage.Remove(PreferenceKeys.HIDDEN_PROFILE_PICKER_USER_IDS);
            return;
        }

        storage.Set(PreferenceKeys.HIDDEN_PROFILE_PICKER_USER_IDS, JsonSerializer.Serialize(ids));
    }

    public static int VisibleCardCount(
        ILocalUserService users,
        ISharedProfileDevicePinService pins,
        ISharedProfileLocalCache cache,
        IDeviceStorageService storage)
    {
        var hidden = Read(storage);
        var userCount = users.GetAll().Count(user => !hidden.Contains(user.IdentityUserId));
        var pinned = pins.GetPinnedGroupIds();
        var groupCount = cache.GetCached().Count(group => pinned.Contains(group.Id));
        return userCount + groupCount;
    }
}
