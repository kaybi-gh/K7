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
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
            return user.DisplayName;

        if (user.IdentityUserId is null)
            return "?";

        var userName = await identityService.GetUserNameAsync(user.IdentityUserId);
        if (!string.IsNullOrWhiteSpace(userName))
            return userName;

        var email = await identityService.GetEmailAsync(user.IdentityUserId);
        if (!string.IsNullOrWhiteSpace(email))
            return email;

        return "?";
    }
}
