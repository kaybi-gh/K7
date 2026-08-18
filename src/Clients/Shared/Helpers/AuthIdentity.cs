using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Resolves the signed-in identity and whether the session can call authenticated APIs.
/// Offline continue uses authentication type <see cref="OfflineAuthenticationType"/> with no Bearer.
/// </summary>
public static class AuthIdentity
{
    public const string OfflineAuthenticationType = "Offline";

    public static string? GetUserId(ClaimsPrincipal? user)
    {
        if (user is null)
            return null;

        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;
        return string.IsNullOrEmpty(value) ? null : value;
    }

    public static bool IsOnlineAuthenticated(ClaimsPrincipal? user) =>
        user?.Identity?.IsAuthenticated == true
        && !string.Equals(user.Identity.AuthenticationType, OfflineAuthenticationType, StringComparison.Ordinal)
        && GetUserId(user) is not null;

    public static async Task<string?> GetOnlineUserIdAsync(
        AuthenticationStateProvider auth,
        CancellationToken cancellationToken = default)
    {
        var state = await auth.GetAuthenticationStateAsync().WaitAsync(cancellationToken);
        return IsOnlineAuthenticated(state.User) ? GetUserId(state.User) : null;
    }
}
