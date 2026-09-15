using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.CustomNav;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.CustomNav;

namespace K7.Server.Application.Features.CustomNav.Queries.GetEffectiveCustomNavLayout;

public record GetEffectiveCustomNavLayoutQuery : IRequest<CustomNavLayoutDto>;

public class GetEffectiveCustomNavLayoutQueryHandler(
    IUserSettingsService userSettingsService,
    IServerSettingsService serverSettingsService,
    IApplicationDbContext context,
    IIdentityService identityService,
    IUser currentUser)
    : IRequestHandler<GetEffectiveCustomNavLayoutQuery, CustomNavLayoutDto>
{
    public async Task<CustomNavLayoutDto> Handle(
        GetEffectiveCustomNavLayoutQuery request,
        CancellationToken cancellationToken)
    {
        if (currentUser.Id is { } userId)
        {
            var userJson = await userSettingsService.GetAsync(userId, UserSettingKeys.CustomNavLayout, cancellationToken);
            if (userJson is not null)
            {
                return await CustomNavAccess.SanitizeAsync(
                    context,
                    identityService,
                    CustomNavLayoutParser.ParseOrDefault(userJson),
                    userId,
                    cancellationToken);
            }
        }

        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.CustomNavLayout, cancellationToken);
        if (serverJson is not null)
        {
            return await CustomNavAccess.SanitizeAsync(
                context,
                identityService,
                CustomNavLayoutParser.ParseOrDefault(serverJson),
                currentUser.Id,
                cancellationToken);
        }

        return CustomNavLayoutDto.Disabled();
    }
}
