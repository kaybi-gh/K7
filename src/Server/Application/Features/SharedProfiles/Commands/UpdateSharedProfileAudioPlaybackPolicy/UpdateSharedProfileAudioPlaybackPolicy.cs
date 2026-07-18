using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfileAudioPlaybackPolicy;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record UpdateSharedProfileAudioPlaybackPolicyCommand : IRequest
{
    public required Guid SharedProfileId { get; init; }
    public required AudioPlaybackPolicySettingsDto Settings { get; init; }
}

public class UpdateSharedProfileAudioPlaybackPolicyCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService,
    ISharedProfileSettingsService sharedProfileSettingsService)
    : IRequestHandler<UpdateSharedProfileAudioPlaybackPolicyCommand>
{
    public async Task Handle(UpdateSharedProfileAudioPlaybackPolicyCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await SharedProfileMemberValidator.GetGroupForHostAsync(
            context, identityService, request.SharedProfileId, userId, currentUser.IdentityId, cancellationToken);

        var json = JsonSerializer.Serialize(request.Settings);
        await sharedProfileSettingsService.SetAsync(
            request.SharedProfileId, UserSettingKeys.AudioPlaybackPolicy, json, cancellationToken);
    }
}
