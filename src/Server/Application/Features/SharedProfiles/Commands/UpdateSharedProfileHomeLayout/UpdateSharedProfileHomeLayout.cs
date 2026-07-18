using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Home;

namespace K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfileHomeLayout;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record UpdateSharedProfileHomeLayoutCommand : IRequest
{
    public required Guid SharedProfileId { get; init; }
    public required HomeLayoutDto Layout { get; init; }
}

public class UpdateSharedProfileHomeLayoutCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService,
    ISharedProfileSettingsService sharedProfileSettingsService)
    : IRequestHandler<UpdateSharedProfileHomeLayoutCommand>
{
    public async Task Handle(UpdateSharedProfileHomeLayoutCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await SharedProfileMemberValidator.GetGroupForHostAsync(
            context, identityService, request.SharedProfileId, userId, currentUser.IdentityId, cancellationToken);

        var json = JsonSerializer.Serialize(request.Layout);
        await sharedProfileSettingsService.SetAsync(
            request.SharedProfileId, UserSettingKeys.HomeLayout, json, cancellationToken);
    }
}
