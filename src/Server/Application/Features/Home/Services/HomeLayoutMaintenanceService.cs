using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Home;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Home.Services;

public class HomeLayoutMaintenanceService(IApplicationDbContext context) : IHomeLayoutMaintenanceService
{
    public async Task RemoveLibraryReferencesAsync(Guid deletedLibraryId, CancellationToken cancellationToken = default)
    {
        var validLibraryIds = await context.Libraries
            .AsNoTracking()
            .Where(l => l.Id != deletedLibraryId)
            .Select(l => l.Id)
            .ToHashSetAsync(cancellationToken);

        await UpdateStoredLayoutsAsync(validLibraryIds, cancellationToken);
    }

    public async Task<HomeLayoutDto> SanitizeAsync(HomeLayoutDto layout, CancellationToken cancellationToken = default)
    {
        var validLibraryIds = await context.Libraries
            .AsNoTracking()
            .Select(l => l.Id)
            .ToHashSetAsync(cancellationToken);

        return HomeLayoutSanitizer.Sanitize(layout, validLibraryIds);
    }

    private async Task UpdateStoredLayoutsAsync(HashSet<Guid> validLibraryIds, CancellationToken cancellationToken)
    {
        var serverSetting = await context.ServerSettings
            .FirstOrDefaultAsync(s => s.Key == ServerSettingKeys.HomeLayout.Name, cancellationToken);

        if (serverSetting is not null)
            TryUpdateSetting(serverSetting, validLibraryIds);

        var userSettings = await context.UserSettings
            .Where(s => s.Key == UserSettingKeys.HomeLayout.Name)
            .ToListAsync(cancellationToken);

        foreach (var setting in userSettings)
            TryUpdateSetting(setting, validLibraryIds);
    }

    private static void TryUpdateSetting(Domain.Entities.Settings.BaseSetting setting, HashSet<Guid> validLibraryIds)
    {
        var layout = JsonSerializer.Deserialize<HomeLayoutDto>(setting.Value);
        if (layout is null)
            return;

        var sanitized = HomeLayoutSanitizer.Sanitize(layout, validLibraryIds);
        if (!HomeLayoutSanitizer.HasChanges(layout, sanitized))
            return;

        setting.Value = JsonSerializer.Serialize(sanitized);
    }
}
