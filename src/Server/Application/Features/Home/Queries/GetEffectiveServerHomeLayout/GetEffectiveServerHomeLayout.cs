using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using K7.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Home.Queries.GetEffectiveServerHomeLayout;

[Authorize(Roles = Roles.Administrator)]
public record GetEffectiveServerHomeLayoutQuery : IRequest<HomeLayoutDto>;

public class GetEffectiveServerHomeLayoutQueryHandler(
    IServerSettingsService serverSettingsService,
    IApplicationDbContext context)
    : IRequestHandler<GetEffectiveServerHomeLayoutQuery, HomeLayoutDto>
{
    public async Task<HomeLayoutDto> Handle(GetEffectiveServerHomeLayoutQuery request, CancellationToken cancellationToken)
    {
        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.HomeLayout, cancellationToken);
        if (serverJson is not null)
        {
            var layout = JsonSerializer.Deserialize<HomeLayoutDto>(serverJson);
            if (layout is not null)
                return layout;
        }

        return await BuildDynamicDefaultAsync(cancellationToken);
    }

    private async Task<HomeLayoutDto> BuildDynamicDefaultAsync(CancellationToken cancellationToken)
    {
        var libraries = await context.Libraries
            .OrderBy(l => l.Title)
            .Select(l => new { l.Id, l.Title })
            .ToListAsync(cancellationToken);

        var rows = new List<HomeRowConfigDto>
        {
            new()
            {
                Id = new Guid("00000000-0000-0000-0000-000000000001"),
                Title = "ContinueWatching",
                DisplayType = HomeRowDisplayType.Carousel,
                ContinueWatching = true,
                OrderBy = [MediaOrderingOption.LastInteractedDesc],
                PageSize = 20,
                IsVisible = true,
                Order = 0
            }
        };

        var order = 1;
        foreach (var lib in libraries)
        {
            rows.Add(new HomeRowConfigDto
            {
                Id = lib.Id,
                Title = lib.Title,
                DisplayType = HomeRowDisplayType.Carousel,
                ContinueWatching = false,
                LibraryIds = [lib.Id],
                OrderBy = [MediaOrderingOption.CreatedDesc],
                PageSize = 50,
                IsVisible = true,
                Order = order++
            });
        }

        return new HomeLayoutDto { Rows = rows };
    }
}
