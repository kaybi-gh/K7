using System.Text.Json;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Home;

namespace K7.Server.Application.Features.Home.Queries.GetDefaultHomeLayout;

[Authorize(Roles = Roles.Administrator)]
public record GetDefaultHomeLayoutQuery : IRequest<HomeLayoutDto?>;

public class GetDefaultHomeLayoutQueryHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<GetDefaultHomeLayoutQuery, HomeLayoutDto?>
{
    public async Task<HomeLayoutDto?> Handle(GetDefaultHomeLayoutQuery request, CancellationToken cancellationToken)
    {
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.HomeLayout, cancellationToken);
        return json is not null ? JsonSerializer.Deserialize<HomeLayoutDto>(json) : null;
    }
}
