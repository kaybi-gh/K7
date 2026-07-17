using K7.Shared.Dtos;
using K7.Shared.Dtos.Home;
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
    Task<VideoPlaybackPolicySettingsDto> GetVideoPlaybackPolicyAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateVideoPlaybackPolicyAsync(Guid id, VideoPlaybackPolicySettingsDto settings, CancellationToken cancellationToken = default);
    Task<AudioPlaybackPolicySettingsDto> GetAudioPlaybackPolicyAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAudioPlaybackPolicyAsync(Guid id, AudioPlaybackPolicySettingsDto settings, CancellationToken cancellationToken = default);
    Task AssignContentRestrictionAsync(Guid id, Guid? contentRestrictionProfileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> GetPlaylistIdsAsync(Guid id, CancellationToken cancellationToken = default);
    Task SharePlaylistAsync(Guid id, Guid playlistId, CancellationToken cancellationToken = default);
    Task UnsharePlaylistAsync(Guid id, Guid playlistId, CancellationToken cancellationToken = default);
    Task<HomeLayoutDto?> GetHomeLayoutAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateHomeLayoutAsync(Guid id, HomeLayoutDto layout, CancellationToken cancellationToken = default);
    Task DeleteHomeLayoutAsync(Guid id, CancellationToken cancellationToken = default);
}
