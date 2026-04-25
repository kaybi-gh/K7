using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using K7.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Home.Queries.GetEffectiveHomeLayout;

public record GetEffectiveHomeLayoutQuery : IRequest<HomeLayoutDto>;

public class GetEffectiveHomeLayoutQueryHandler(
    IUserSettingsService userSettingsService,
    IServerSettingsService serverSettingsService,
    IApplicationDbContext context,
    IUser currentUser)
    : IRequestHandler<GetEffectiveHomeLayoutQuery, HomeLayoutDto>
{
    public async Task<HomeLayoutDto> Handle(GetEffectiveHomeLayoutQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is { } userId)
        {
            var userJson = await userSettingsService.GetAsync(userId, UserSettingKeys.HomeLayout, cancellationToken);
            if (userJson is not null)
                return JsonSerializer.Deserialize<HomeLayoutDto>(userJson) ?? await BuildDynamicDefaultAsync(userId, cancellationToken);
        }

        var serverJson = await serverSettingsService.GetAsync(ServerSettingKeys.HomeLayout, cancellationToken);
        if (serverJson is not null)
            return JsonSerializer.Deserialize<HomeLayoutDto>(serverJson) ?? await BuildDynamicDefaultAsync(currentUser.Id, cancellationToken);

        return await BuildDynamicDefaultAsync(currentUser.Id, cancellationToken);
    }

    private async Task<HomeLayoutDto> BuildDynamicDefaultAsync(Guid? userId, CancellationToken cancellationToken)
    {
        var libraries = await context.Libraries
            .Where(l => !context.UserLibraryExclusions.Any(e =>
                e.LibraryId == l.Id &&
                e.UserId == (userId ?? Guid.Empty) &&
                (e.IsAdminExcluded || e.IsSelfExcluded)))
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
