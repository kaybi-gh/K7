using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using K7.Shared.Enums;
using K7.Shared.Home;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Home.Queries.GetEffectiveHomeLayout;

public record GetEffectiveHomeLayoutQuery : IRequest<HomeLayoutDto>;

public class GetEffectiveHomeLayoutQueryHandler(
    IUserSettingsService userSettingsService,
    IServerSettingsService serverSettingsService,
    IApplicationDbContext context,
    IHomeLayoutMaintenanceService homeLayoutMaintenanceService,
    IHomeRecommendationService homeRecommendationService,
    IUser currentUser)
    : IRequestHandler<GetEffectiveHomeLayoutQuery, HomeLayoutDto>
{
    public async Task<HomeLayoutDto> Handle(GetEffectiveHomeLayoutQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is { } userId)
        {
            var userJson = await userSettingsService.GetAsync(userId, UserSettingKeys.HomeLayout, cancellationToken);
            if (userJson is not null)
            {
                var layout = JsonSerializer.Deserialize<HomeLayoutDto>(userJson);
                if (layout is not null)
                    return await homeLayoutMaintenanceService.SanitizeAsync(layout, cancellationToken);
            }
        }

        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.HomeLayout, cancellationToken);
        if (serverJson is not null)
        {
            var layout = JsonSerializer.Deserialize<HomeLayoutDto>(serverJson);
            if (layout is not null)
                return await homeLayoutMaintenanceService.SanitizeAsync(layout, cancellationToken);
        }

        return await BuildDynamicDefaultAsync(currentUser.Id, cancellationToken);
    }

    private async Task<HomeLayoutDto> BuildDynamicDefaultAsync(Guid? userId, CancellationToken cancellationToken)
    {
        var groups = await context.LibraryGroups
            .Include(g => g.Libraries)
            .Where(g => g.Libraries.Any(l => !context.UserLibraryExclusions.Any(e =>
                e.LibraryId == l.Id &&
                e.UserId == (userId ?? Guid.Empty) &&
                (e.IsAdminExcluded || e.IsSelfExcluded))))
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
                PageSize = PagingDefaults.DefaultPageSize,
                IsVisible = true,
                Order = 0
            },
            new()
            {
                Id = new Guid("00000000-0000-0000-0000-000000000002"),
                Title = HomeLayoutRowTitles.RecommendedForYou,
                DisplayType = HomeRowDisplayType.Carousel,
                ContinueWatching = false,
                OrderBy = [MediaOrderingOption.RecommendedForYou],
                PageSize = PagingDefaults.DefaultPageSize,
                IsVisible = true,
                Order = 1
            }
        };

        var order = 2;

        if (userId.HasValue)
        {
            var becauseYouWatchedTitle = await homeRecommendationService.GetBecauseYouWatchedTitleAsync(userId.Value, cancellationToken);
            if (becauseYouWatchedTitle is not null)
            {
                rows.Add(new HomeRowConfigDto
                {
                    Id = new Guid("00000000-0000-0000-0000-000000000003"),
                    Title = HomeLayoutRowTitles.BecauseYouWatched(becauseYouWatchedTitle),
                    DisplayType = HomeRowDisplayType.Carousel,
                    ContinueWatching = false,
                    OrderBy = [MediaOrderingOption.BecauseYouWatched],
                    PageSize = PagingDefaults.DefaultPageSize,
                    IsVisible = true,
                    Order = order++
                });
            }
        }

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
                PageSize = PagingDefaults.ItemsPageSize,
                IsVisible = true,
                Order = order++
            });
        }

        return new HomeLayoutDto { Rows = rows };
    }
}
