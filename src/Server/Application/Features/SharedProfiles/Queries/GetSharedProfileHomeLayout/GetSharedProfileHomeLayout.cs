using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Home;

namespace K7.Server.Application.Features.SharedProfiles.Queries.GetSharedProfileHomeLayout;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetSharedProfileHomeLayoutQuery(Guid SharedProfileId) : IRequest<HomeLayoutDto?>;

public class GetSharedProfileHomeLayoutQueryHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService,
    ISharedProfileSettingsService sharedProfileSettingsService)
    : IRequestHandler<GetSharedProfileHomeLayoutQuery, HomeLayoutDto?>
{
    public async Task<HomeLayoutDto?> Handle(GetSharedProfileHomeLayoutQuery request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await SharedProfileMemberValidator.GetGroupForHostAsync(
            context, identityService, request.SharedProfileId, userId, currentUser.IdentityId, cancellationToken);

        var json = await sharedProfileSettingsService.GetAsync(
            request.SharedProfileId, UserSettingKeys.HomeLayout, cancellationToken);

        return json is not null ? JsonSerializer.Deserialize<HomeLayoutDto>(json) : null;
    }
}
