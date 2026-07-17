using K7.Shared.Dtos;
using K7.Shared.Dtos.Home;
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
    Task<VideoPlaybackPolicySettingsDto> GetSharedProfileVideoPlaybackPolicyAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateSharedProfileVideoPlaybackPolicyAsync(Guid id, VideoPlaybackPolicySettingsDto settings, CancellationToken cancellationToken = default);
    Task<AudioPlaybackPolicySettingsDto> GetSharedProfileAudioPlaybackPolicyAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateSharedProfileAudioPlaybackPolicyAsync(Guid id, AudioPlaybackPolicySettingsDto settings, CancellationToken cancellationToken = default);
    Task AssignSharedProfileContentRestrictionAsync(Guid id, Guid? contentRestrictionProfileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetSharedProfilePlaylistIdsAsync(Guid id, CancellationToken cancellationToken = default);
    Task SharePlaylistToSharedProfileAsync(Guid id, Guid playlistId, CancellationToken cancellationToken = default);
    Task UnsharePlaylistFromSharedProfileAsync(Guid id, Guid playlistId, CancellationToken cancellationToken = default);
    Task<HomeLayoutDto?> GetSharedProfileHomeLayoutAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateSharedProfileHomeLayoutAsync(Guid id, HomeLayoutDto layout, CancellationToken cancellationToken = default);
    Task DeleteSharedProfileHomeLayoutAsync(Guid id, CancellationToken cancellationToken = default);
}
