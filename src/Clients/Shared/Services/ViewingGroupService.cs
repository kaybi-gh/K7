using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.ViewingGroups;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public class ViewingGroupService(IViewingGroupApi api, IViewingGroupLocalCache cache) : IViewingGroupService
{
    public async Task<IReadOnlyList<ViewingGroupDto>> GetViewingGroupsAsync(CancellationToken cancellationToken = default)
    {
        var groups = await api.GetViewingGroupsAsync(cancellationToken);
        cache.UpdateCache(groups);
        return groups;
    }

    public Task<IReadOnlyList<ViewingGroupMemberCandidateDto>> GetMemberCandidatesAsync(CancellationToken cancellationToken = default) =>
        api.GetViewingGroupMemberCandidatesAsync(cancellationToken);

    public async Task<Guid> CreateAsync(CreateViewingGroupRequest request, CancellationToken cancellationToken = default)
    {
        var id = await api.CreateViewingGroupAsync(request, cancellationToken);
        await cache.RefreshAsync(cancellationToken);
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateViewingGroupRequest request, CancellationToken cancellationToken = default)
    {
        await api.UpdateViewingGroupAsync(id, request, cancellationToken);
        await cache.RefreshAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await api.DeleteViewingGroupAsync(id, cancellationToken);
        await cache.RefreshAsync(cancellationToken);
    }

    public async Task SetPinAsync(Guid id, string? pin, CancellationToken cancellationToken = default)
    {
        await api.SetViewingGroupPinAsync(id, new SetViewingGroupPinRequest { Pin = pin }, cancellationToken);
        await cache.RefreshAsync(cancellationToken);
    }

    public bool VerifyGroupPin(ViewingGroupDto group, string pin) =>
        PinVerifier.Verify(group.PinHash, pin);
}
