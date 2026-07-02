using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.ViewingGroups;

namespace K7.Shared.Interfaces;

public interface IViewingGroupApi
{
    Task<IReadOnlyList<ViewingGroupDto>> GetViewingGroupsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ViewingGroupMemberCandidateDto>> GetViewingGroupMemberCandidatesAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateViewingGroupAsync(CreateViewingGroupRequest request, CancellationToken cancellationToken = default);
    Task UpdateViewingGroupAsync(Guid id, UpdateViewingGroupRequest request, CancellationToken cancellationToken = default);
    Task DeleteViewingGroupAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetViewingGroupPinAsync(Guid id, SetViewingGroupPinRequest request, CancellationToken cancellationToken = default);
}
