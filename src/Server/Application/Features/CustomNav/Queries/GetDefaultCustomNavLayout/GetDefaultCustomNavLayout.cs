using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.CustomNav;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.CustomNav;

namespace K7.Server.Application.Features.CustomNav.Queries.GetDefaultCustomNavLayout;

[Authorize(Roles = Roles.Administrator)]
public record GetDefaultCustomNavLayoutQuery : IRequest<CustomNavLayoutDto?>;

public class GetDefaultCustomNavLayoutQueryHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<GetDefaultCustomNavLayoutQuery, CustomNavLayoutDto?>
{
    public async Task<CustomNavLayoutDto?> Handle(
        GetDefaultCustomNavLayoutQuery request,
        CancellationToken cancellationToken)
    {
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.CustomNavLayout, cancellationToken);
        return json is not null ? CustomNavLayoutParser.ParseOrDefault(json) : null;
    }
}
