using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.CustomNav;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.CustomNav;
using K7.Shared.Dtos.CustomNav;

namespace K7.Server.Application.Features.CustomNav.Queries.GetEffectiveServerCustomNavLayout;

[Authorize(Roles = Roles.Administrator)]
public record GetEffectiveServerCustomNavLayoutQuery : IRequest<CustomNavLayoutDto>;

public class GetEffectiveServerCustomNavLayoutQueryHandler(
    IServerSettingsService serverSettingsService,
    IApplicationDbContext context)
    : IRequestHandler<GetEffectiveServerCustomNavLayoutQuery, CustomNavLayoutDto>
{
    public async Task<CustomNavLayoutDto> Handle(
        GetEffectiveServerCustomNavLayoutQuery request,
        CancellationToken cancellationToken)
    {
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.CustomNavLayout, cancellationToken);
        if (json is null)
            return CustomNavLayoutDto.Disabled();

        var groupIds = await CustomNavAccess.GetAccessibleLibraryGroupIdsAsync(context, null, cancellationToken);
        return CustomNavSanitizer.Sanitize(
            CustomNavLayoutParser.ParseOrDefault(json),
            groupIds,
            null,
            null,
            allowAdminRoutes: true);
    }
}
