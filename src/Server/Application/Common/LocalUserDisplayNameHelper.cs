using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Common;

internal static class LocalUserDisplayNameHelper
{
    public static async Task<string> ResolveAsync(
        IIdentityService identityService,
        User user,
        CancellationToken cancellationToken = default)
    {
        var resolved = await ResolveManyAsync(identityService, [user], cancellationToken);
        return resolved.GetValueOrDefault(user.Id) ?? "?";
    }

    public static async Task<IReadOnlyDictionary<Guid, string>> ResolveManyAsync(
        IIdentityService identityService,
        IReadOnlyList<User> users,
        CancellationToken cancellationToken = default)
    {
        if (users.Count == 0)
            return new Dictionary<Guid, string>();

        var identityIds = users
            .Where(u => string.IsNullOrWhiteSpace(u.DisplayName) && u.IdentityUserId is not null)
            .Select(u => u.IdentityUserId!)
            .Distinct()
            .ToList();

        var userNames = identityIds.Count > 0
            ? await identityService.GetUserNamesAsync(identityIds)
            : new Dictionary<string, string?>();

        var missingEmailIds = identityIds
            .Where(id => string.IsNullOrWhiteSpace(userNames.GetValueOrDefault(id)))
            .ToList();

        var emails = missingEmailIds.Count > 0
            ? await identityService.GetEmailsAsync(missingEmailIds)
            : new Dictionary<string, string?>();

        return users.ToDictionary(
            u => u.Id,
            u =>
            {
                if (!string.IsNullOrWhiteSpace(u.DisplayName))
                    return u.DisplayName;

                if (u.IdentityUserId is null)
                    return "?";

                var userName = userNames.GetValueOrDefault(u.IdentityUserId);
                if (!string.IsNullOrWhiteSpace(userName))
                    return userName;

                var email = emails.GetValueOrDefault(u.IdentityUserId);
                return string.IsNullOrWhiteSpace(email) ? "?" : email;
            });
    }
}
