using K7.Shared.Dtos.SharedProfiles;

namespace K7.Clients.Shared.Interfaces;

public interface ISharedProfileSessionService
{
    Guid? ActiveGroupId { get; }
    SharedProfileDto? ActiveGroup { get; }
    event Action? ActiveGroupChanged;
    void SetActiveGroup(SharedProfileDto? group);
    void ClearActiveGroup();
    Task RestoreLastActiveAsync(CancellationToken cancellationToken = default);
}
