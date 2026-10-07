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
        var validLibraryGroupIds = await GetValidLibraryGroupIdsAsync(cancellationToken);

        await UpdateStoredLayoutsAsync(validLibraryIds, validLibraryGroupIds, cancellationToken);
    }

    public async Task RemoveLibraryGroupReferencesAsync(Guid deletedLibraryGroupId, CancellationToken cancellationToken = default)
    {
        var validLibraryIds = await context.Libraries
            .AsNoTracking()
            .Select(l => l.Id)
            .ToHashSetAsync(cancellationToken);
        var validLibraryGroupIds = await context.LibraryGroups
            .AsNoTracking()
            .Where(g => g.Id != deletedLibraryGroupId)
            .Select(g => g.Id)
            .ToHashSetAsync(cancellationToken);

        await UpdateStoredLayoutsAsync(validLibraryIds, validLibraryGroupIds, cancellationToken);
    }

    public async Task<HomeLayoutDto> SanitizeAsync(HomeLayoutDto layout, CancellationToken cancellationToken = default)
    {
        var validLibraryIds = await context.Libraries
            .AsNoTracking()
            .Select(l => l.Id)
            .ToHashSetAsync(cancellationToken);
        var validLibraryGroupIds = await GetValidLibraryGroupIdsAsync(cancellationToken);

        return HomeLayoutSanitizer.Sanitize(layout, validLibraryIds, validLibraryGroupIds);
    }

    private async Task<HashSet<Guid>> GetValidLibraryGroupIdsAsync(CancellationToken cancellationToken) =>
        await context.LibraryGroups
            .AsNoTracking()
            .Select(g => g.Id)
            .ToHashSetAsync(cancellationToken);

    private async Task UpdateStoredLayoutsAsync(
        HashSet<Guid> validLibraryIds,
        HashSet<Guid> validLibraryGroupIds,
        CancellationToken cancellationToken)
    {
        var serverSetting = await context.ServerSettings
            .FirstOrDefaultAsync(s => s.Key == ServerSettingKeys.HomeLayout.Name, cancellationToken);

        if (serverSetting is not null)
            TryUpdateSetting(serverSetting, validLibraryIds, validLibraryGroupIds);

        var userSettings = await context.UserSettings
            .Where(s => s.Key == UserSettingKeys.HomeLayout.Name)
            .ToListAsync(cancellationToken);

        foreach (var setting in userSettings)
            TryUpdateSetting(setting, validLibraryIds, validLibraryGroupIds);

        var sharedProfileSettings = await context.SharedProfileSettings
            .Where(s => s.Key == UserSettingKeys.HomeLayout.Name)
            .ToListAsync(cancellationToken);

        foreach (var setting in sharedProfileSettings)
            TryUpdateSetting(setting, validLibraryIds, validLibraryGroupIds);
    }

    private static void TryUpdateSetting(
        Domain.Entities.Settings.BaseSetting setting,
        HashSet<Guid> validLibraryIds,
        HashSet<Guid> validLibraryGroupIds)
    {
        if (!HomeLayoutSettingSerializer.TryDeserialize(setting.Value, out var layout) || layout is null)
            return;

        var sanitized = HomeLayoutSanitizer.Sanitize(layout, validLibraryIds, validLibraryGroupIds);
        if (!HomeLayoutSanitizer.HasChanges(layout, sanitized))
            return;

        setting.Value = HomeLayoutSettingSerializer.Serialize(sanitized);
    }
}
