using K7.Shared.Dtos.Requests;
using K7.Shared.Dtos.SharedProfiles;

namespace K7.Shared.Interfaces;

public interface ISharedProfileApi
{
    Task<IReadOnlyList<SharedProfileDto>> GetSharedProfilesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SharedProfileMemberCandidateDto>> GetSharedProfileMemberCandidatesAsync(CancellationToken cancellationToken = default);
    Task<Guid> CreateSharedProfileAsync(CreateSharedProfileRequest request, CancellationToken cancellationToken = default);
    Task UpdateSharedProfileAsync(Guid id, UpdateSharedProfileRequest request, CancellationToken cancellationToken = default);
    Task DeleteSharedProfileAsync(Guid id, CancellationToken cancellationToken = default);
    Task SetSharedProfilePinAsync(Guid id, SetSharedProfilePinRequest request, CancellationToken cancellationToken = default);
    Task<bool> VerifySharedProfilePinAsync(Guid id, string pin, CancellationToken cancellationToken = default);
    Task LeaveSharedProfileAsync(Guid id, LeaveSharedProfileRequest request, CancellationToken cancellationToken = default);
}
