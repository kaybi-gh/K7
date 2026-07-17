using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.SharedProfiles.Commands.DeleteSharedProfileHomeLayout;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record DeleteSharedProfileHomeLayoutCommand(Guid SharedProfileId) : IRequest;

public class DeleteSharedProfileHomeLayoutCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService,
    ISharedProfileSettingsService sharedProfileSettingsService)
    : IRequestHandler<DeleteSharedProfileHomeLayoutCommand>
{
    public async Task Handle(DeleteSharedProfileHomeLayoutCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await SharedProfileMemberValidator.GetGroupForHostAsync(
            context, identityService, request.SharedProfileId, userId, currentUser.IdentityId, cancellationToken);

        await sharedProfileSettingsService.RemoveAsync(
            request.SharedProfileId, UserSettingKeys.HomeLayout, cancellationToken);
    }
}
