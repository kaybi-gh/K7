using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.SharedProfiles.Queries.GetSharedProfileVideoPlaybackPolicy;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetSharedProfileVideoPlaybackPolicyQuery(Guid SharedProfileId) : IRequest<VideoPlaybackPolicySettingsDto>;

public class GetSharedProfileVideoPlaybackPolicyQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    ISharedProfileSettingsService sharedProfileSettingsService,
    IServerSettingsService serverSettingsService)
    : IRequestHandler<GetSharedProfileVideoPlaybackPolicyQuery, VideoPlaybackPolicySettingsDto>
{
    public async Task<VideoPlaybackPolicySettingsDto> Handle(
        GetSharedProfileVideoPlaybackPolicyQuery request,
        CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await SharedProfileMemberValidator.GetGroupForMemberAsync(
            context, request.SharedProfileId, userId, cancellationToken);

        var json = await sharedProfileSettingsService.GetAsync(
            request.SharedProfileId, UserSettingKeys.VideoPlaybackPolicy, cancellationToken);
        if (json is not null)
            return JsonSerializer.Deserialize<VideoPlaybackPolicySettingsDto>(json) ?? new VideoPlaybackPolicySettingsDto();

        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.VideoPlaybackPolicy, cancellationToken);
        if (serverJson is not null)
            return JsonSerializer.Deserialize<VideoPlaybackPolicySettingsDto>(serverJson) ?? new VideoPlaybackPolicySettingsDto();

        return new VideoPlaybackPolicySettingsDto();
    }
}
