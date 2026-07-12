using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.SharedProfiles;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public class SharedProfileService(
    ISharedProfileApi api,
    ISharedProfileLocalCache cache,
    ISharedProfileSessionService? session = null) : ISharedProfileService
{
    public async Task<IReadOnlyList<SharedProfileDto>> GetSharedProfilesAsync(CancellationToken cancellationToken = default)
    {
        var groups = await api.GetSharedProfilesAsync(cancellationToken);
        cache.UpdateCache(groups);
        return groups;
    }

    public Task<IReadOnlyList<SharedProfileMemberCandidateDto>> GetMemberCandidatesAsync(CancellationToken cancellationToken = default) =>
        api.GetSharedProfileMemberCandidatesAsync(cancellationToken);

    public async Task<Guid> CreateAsync(CreateSharedProfileRequest request, CancellationToken cancellationToken = default)
    {
        var id = await api.CreateSharedProfileAsync(request, cancellationToken);
        await cache.RefreshAsync(cancellationToken);
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateSharedProfileRequest request, CancellationToken cancellationToken = default)
    {
        await api.UpdateSharedProfileAsync(id, request, cancellationToken);
        await cache.RefreshAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await api.DeleteSharedProfileAsync(id, cancellationToken);
        await cache.RefreshAsync(cancellationToken);
    }

    public async Task SetPinAsync(Guid id, string? pin, CancellationToken cancellationToken = default)
    {
        await api.SetSharedProfilePinAsync(id, new SetSharedProfilePinRequest { Pin = pin }, cancellationToken);
        await cache.RefreshAsync(cancellationToken);
    }

    public async Task LeaveAsync(Guid id, Guid? newHostUserId = null, CancellationToken cancellationToken = default)
    {
        await api.LeaveSharedProfileAsync(id, new LeaveSharedProfileRequest { NewHostUserId = newHostUserId }, cancellationToken);
        if (session?.ActiveGroupId == id)
            session.ClearActiveGroup();

        await cache.RefreshAsync(cancellationToken);
    }

    public Task<bool> VerifyGroupPinAsync(SharedProfileDto group, string pin, CancellationToken cancellationToken = default) =>
        api.VerifySharedProfilePinAsync(group.Id, pin, cancellationToken);
}
