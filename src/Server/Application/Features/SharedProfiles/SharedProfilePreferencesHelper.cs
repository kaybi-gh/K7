using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.SharedProfiles;

internal static class SharedProfilePreferencesHelper
{
    internal static async Task<HashSet<Guid>> GetUsersBlockingMembershipAsync(
        IApplicationDbContext context,
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
            return [];

        var key = UserSettingKeys.SharedProfilePreferences.Name;
        var settings = await context.UserSettings
            .AsNoTracking()
            .Where(s => userIds.Contains(s.UserId) && s.Key == key)
            .Select(s => new { s.UserId, s.Value })
            .ToListAsync(cancellationToken);

        var blocked = new HashSet<Guid>();
        foreach (var setting in settings)
        {
            if (string.IsNullOrEmpty(setting.Value))
                continue;

            var prefs = JsonSerializer.Deserialize<SharedProfilePreferencesDto>(setting.Value);
            if (prefs?.BlockNewMembership == true)
                blocked.Add(setting.UserId);
        }

        return blocked;
    }
}
