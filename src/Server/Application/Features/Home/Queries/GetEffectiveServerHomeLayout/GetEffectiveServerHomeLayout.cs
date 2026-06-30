using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using K7.Shared.Enums;
using K7.Shared.Home;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Home.Queries.GetEffectiveServerHomeLayout;

[Authorize(Roles = Roles.Administrator)]
public record GetEffectiveServerHomeLayoutQuery : IRequest<HomeLayoutDto>;

public class GetEffectiveServerHomeLayoutQueryHandler(
    IServerSettingsService serverSettingsService,
    IApplicationDbContext context,
    IHomeLayoutMaintenanceService homeLayoutMaintenanceService)
    : IRequestHandler<GetEffectiveServerHomeLayoutQuery, HomeLayoutDto>
{
    public async Task<HomeLayoutDto> Handle(GetEffectiveServerHomeLayoutQuery request, CancellationToken cancellationToken)
    {
        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.HomeLayout, cancellationToken);
        if (serverJson is not null)
        {
            var layout = JsonSerializer.Deserialize<HomeLayoutDto>(serverJson);
            if (layout is not null)
                return await homeLayoutMaintenanceService.SanitizeAsync(layout, cancellationToken);
        }

        return await BuildDynamicDefaultAsync(cancellationToken);
    }

    private async Task<HomeLayoutDto> BuildDynamicDefaultAsync(CancellationToken cancellationToken)
    {
        var groups = await context.LibraryGroups
            .Include(g => g.Libraries)
            .OrderBy(g => g.Title)
            .Select(g => new { g.Id, g.Title, LibraryIds = g.Libraries.Select(l => l.Id).ToList() })
            .ToListAsync(cancellationToken);

        var rows = new List<HomeRowConfigDto>
        {
            new()
            {
                Id = new Guid("00000000-0000-0000-0000-000000000001"),
                Title = HomeLayoutRowTitles.ContinueWatching,
                DisplayType = HomeRowDisplayType.Carousel,
                ContinueWatching = true,
                OrderBy = [MediaOrderingOption.LastInteractedDesc],
                PageSize = 20,
                IsVisible = true,
                Order = 0
            }
        };

        var order = 1;
        foreach (var group in groups)
        {
            rows.Add(new HomeRowConfigDto
            {
                Id = group.Id,
                Title = HomeLayoutRowTitles.NewlyAddedIn(group.Title),
                DisplayType = HomeRowDisplayType.Carousel,
                ContinueWatching = false,
                LibraryIds = group.LibraryIds,
                OrderBy = [MediaOrderingOption.CreatedDesc],
                PageSize = 50,
                IsVisible = true,
                Order = order++
            });
        }

        return new HomeLayoutDto { Rows = rows };
    }
}
