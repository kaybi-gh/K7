using K7.Clients.Shared.Interfaces;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Solo auto-login restore. Must not run until a server URL is applied: otherwise
/// OpenIddict has no K7 registration and HttpClient has no BaseAddress, which used
/// to throw during startup and send the user back to native server setup.
/// </summary>
public static class MauiSessionRestore
{
    public static bool ShouldRestore(ILocalUserService localUsers, bool serverConfigured)
    {
        ArgumentNullException.ThrowIfNull(localUsers);

        if (!serverConfigured || !localUsers.IsSingleUserMode)
            return false;

        var last = localUsers.GetLastActive();
        return last is not null && localUsers.IsSingleUserUnlocked(last.IdentityUserId);
    }
}
