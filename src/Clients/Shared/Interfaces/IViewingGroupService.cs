using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.ViewingGroups;

namespace K7.Clients.Shared.Interfaces;

public interface IViewingGroupService
{
    Task<IReadOnlyList<ViewingGroupDto>> GetViewingGroupsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ViewingGroupMemberCandidateDto>> GetMemberCandidatesAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateViewingGroupRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateViewingGroupRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetPinAsync(Guid id, string? pin, CancellationToken cancellationToken = default);
    bool VerifyGroupPin(ViewingGroupDto group, string pin);
}
