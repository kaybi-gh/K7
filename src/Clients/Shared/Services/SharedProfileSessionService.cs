using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Dtos.SharedProfiles;

namespace K7.Clients.Shared.Services;

public class SharedProfileSessionService(
    IDeviceStorageService storage,
    ISharedProfileLocalCache cache) : ISharedProfileSessionService
{
    private SharedProfileDto? _activeGroup;

    public Guid? ActiveGroupId => _activeGroup?.Id;

    public SharedProfileDto? ActiveGroup => _activeGroup;

    public event Action? ActiveGroupChanged;

    public void SetActiveGroup(SharedProfileDto? group)
    {
        _activeGroup = group;

        if (group is null)
        {
            storage.Remove(PreferenceKeys.ACTIVE_SHARED_PROFILE_ID);
            storage.Remove(PreferenceKeys.LAST_ACTIVE_SHARED_PROFILE_ID);
        }
        else
        {
            storage.Set(PreferenceKeys.ACTIVE_SHARED_PROFILE_ID, group.Id.ToString());
            storage.Set(PreferenceKeys.LAST_ACTIVE_SHARED_PROFILE_ID, group.Id.ToString());
        }

        ActiveGroupChanged?.Invoke();
    }

    public void ClearActiveGroup() => SetActiveGroup(null);

    public Task RestoreLastActiveAsync(CancellationToken cancellationToken = default)
    {
        var lastId = storage.Get(PreferenceKeys.LAST_ACTIVE_SHARED_PROFILE_ID);
        if (string.IsNullOrEmpty(lastId) || !Guid.TryParse(lastId, out var groupId))
        {
            ClearActiveGroup();
            return Task.CompletedTask;
        }

        var group = cache.FindById(groupId);
        if (group is null)
        {
            ClearActiveGroup();
            return Task.CompletedTask;
        }

        _activeGroup = group;
        storage.Set(PreferenceKeys.ACTIVE_SHARED_PROFILE_ID, group.Id.ToString());
        ActiveGroupChanged?.Invoke();
        return Task.CompletedTask;
    }
}
