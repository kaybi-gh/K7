using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Dtos.ViewingGroups;

namespace K7.Clients.Shared.Services;

public class ViewingGroupSessionService(
    IDeviceStorageService storage,
    IViewingGroupLocalCache cache) : IViewingGroupSessionService
{
    private ViewingGroupDto? _activeGroup;

    public Guid? ActiveGroupId => _activeGroup?.Id;

    public ViewingGroupDto? ActiveGroup => _activeGroup;

    public event Action? ActiveGroupChanged;

    public void SetActiveGroup(ViewingGroupDto? group)
    {
        _activeGroup = group;

        if (group is null)
        {
            storage.Remove(PreferenceKeys.ACTIVE_VIEWING_GROUP_ID);
            storage.Remove(PreferenceKeys.LAST_ACTIVE_VIEWING_GROUP_ID);
        }
        else
        {
            storage.Set(PreferenceKeys.ACTIVE_VIEWING_GROUP_ID, group.Id.ToString());
            storage.Set(PreferenceKeys.LAST_ACTIVE_VIEWING_GROUP_ID, group.Id.ToString());
        }

        ActiveGroupChanged?.Invoke();
    }

    public void ClearActiveGroup() => SetActiveGroup(null);

    public Task RestoreLastActiveAsync(CancellationToken cancellationToken = default)
    {
        var lastId = storage.Get(PreferenceKeys.LAST_ACTIVE_VIEWING_GROUP_ID);
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
        storage.Set(PreferenceKeys.ACTIVE_VIEWING_GROUP_ID, group.Id.ToString());
        ActiveGroupChanged?.Invoke();
        return Task.CompletedTask;
    }
}
