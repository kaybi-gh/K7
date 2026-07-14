using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.SharedProfiles;

namespace K7.Clients.Shared.Interfaces;

public interface ISharedProfileService
{
    Task<IReadOnlyList<SharedProfileDto>> GetSharedProfilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SharedProfileMemberCandidateDto>> GetMemberCandidatesAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateSharedProfileRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid id, UpdateSharedProfileRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetPinAsync(Guid id, string? pin, CancellationToken cancellationToken = default);
    Task LeaveAsync(Guid id, Guid? newHostUserId = null, CancellationToken cancellationToken = default);
    Task<bool> VerifyGroupPinAsync(SharedProfileDto group, string pin, CancellationToken cancellationToken = default);
}
