using K7.Shared.Dtos.ViewingGroups;

namespace K7.Clients.Shared.Interfaces;

public interface IViewingGroupSessionService
{
    Guid? ActiveGroupId { get; }
    ViewingGroupDto? ActiveGroup { get; }
    event Action? ActiveGroupChanged;
    void SetActiveGroup(ViewingGroupDto? group);
    void ClearActiveGroup();
    Task RestoreLastActiveAsync(CancellationToken cancellationToken = default);
}
