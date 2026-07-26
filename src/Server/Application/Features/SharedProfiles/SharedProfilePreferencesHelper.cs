using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.SharedProfiles;

internal static class SharedProfilePreferencesHelper
{
    /// <summary>
    /// Users who block new membership. Missing preferences default to blocked
    /// (matches <see cref="SharedProfilePreferencesDto.BlockNewMembership"/>).
    /// </summary>
    internal static async Task<HashSet<Guid>> GetUsersBlockingMembershipAsync(
        IApplicationDbContext context,
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken)
    {
        if (userIds.Count == 0)
            return [];

        var distinctIds = userIds.Distinct().ToList();
        var key = UserSettingKeys.SharedProfilePreferences.Name;
        var settings = await context.UserSettings
            .AsNoTracking()
            .Where(s => distinctIds.Contains(s.UserId) && s.Key == key)
            .Select(s => new { s.UserId, s.Value })
            .ToListAsync(cancellationToken);

        var settingsByUser = settings.ToDictionary(s => s.UserId, s => s.Value);
        var blocked = new HashSet<Guid>();

        foreach (var userId in distinctIds)
        {
            if (!settingsByUser.TryGetValue(userId, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                // Default BlockNewMembership is true when no preference is stored.
                blocked.Add(userId);
                continue;
            }

            var prefs = ParsePreferences(raw);
            if (prefs.BlockNewMembership)
                blocked.Add(userId);
        }

        return blocked;
    }

    /// <summary>
    /// <see cref="IUserSettingsService"/> stores <c>SettingKey&lt;string&gt;</c> values as JSON strings,
    /// so the DB may contain a double-encoded payload (a JSON string wrapping the DTO JSON).
    /// </summary>
    internal static SharedProfilePreferencesDto ParsePreferences(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new SharedProfilePreferencesDto();

        try
        {
            var json = value;
            if (json[0] == '"')
            {
                var unwrapped = JsonSerializer.Deserialize<string>(json);
                if (string.IsNullOrWhiteSpace(unwrapped))
                    return new SharedProfilePreferencesDto();

                json = unwrapped;
            }

            return JsonSerializer.Deserialize<SharedProfilePreferencesDto>(json)
                ?? new SharedProfilePreferencesDto();
        }
        catch (JsonException)
        {
            return new SharedProfilePreferencesDto();
        }
    }
}
