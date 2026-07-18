using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Home;
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

    public Task<VideoPlaybackPolicySettingsDto> GetVideoPlaybackPolicyAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetSharedProfileVideoPlaybackPolicyAsync(id, cancellationToken);

    public Task UpdateVideoPlaybackPolicyAsync(Guid id, VideoPlaybackPolicySettingsDto settings, CancellationToken cancellationToken = default) =>
        api.UpdateSharedProfileVideoPlaybackPolicyAsync(id, settings, cancellationToken);

    public Task<AudioPlaybackPolicySettingsDto> GetAudioPlaybackPolicyAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetSharedProfileAudioPlaybackPolicyAsync(id, cancellationToken);

    public Task UpdateAudioPlaybackPolicyAsync(Guid id, AudioPlaybackPolicySettingsDto settings, CancellationToken cancellationToken = default) =>
        api.UpdateSharedProfileAudioPlaybackPolicyAsync(id, settings, cancellationToken);

    public async Task AssignContentRestrictionAsync(Guid id, Guid? contentRestrictionProfileId, CancellationToken cancellationToken = default)
    {
        await api.AssignSharedProfileContentRestrictionAsync(id, contentRestrictionProfileId, cancellationToken);
        await cache.RefreshAsync(cancellationToken);
    }

    public Task<IReadOnlyList<Guid>> GetPlaylistIdsAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetSharedProfilePlaylistIdsAsync(id, cancellationToken);

    public Task SharePlaylistAsync(Guid id, Guid playlistId, CancellationToken cancellationToken = default) =>
        api.SharePlaylistToSharedProfileAsync(id, playlistId, cancellationToken);

    public Task UnsharePlaylistAsync(Guid id, Guid playlistId, CancellationToken cancellationToken = default) =>
        api.UnsharePlaylistFromSharedProfileAsync(id, playlistId, cancellationToken);

    public Task<HomeLayoutDto?> GetHomeLayoutAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.GetSharedProfileHomeLayoutAsync(id, cancellationToken);

    public Task UpdateHomeLayoutAsync(Guid id, HomeLayoutDto layout, CancellationToken cancellationToken = default) =>
        api.UpdateSharedProfileHomeLayoutAsync(id, layout, cancellationToken);

    public Task DeleteHomeLayoutAsync(Guid id, CancellationToken cancellationToken = default) =>
        api.DeleteSharedProfileHomeLayoutAsync(id, cancellationToken);
}
