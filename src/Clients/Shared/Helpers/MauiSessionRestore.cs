using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;

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

    /// <summary>
    /// Android Auto / CarPlay bind the media session without the phone UI. Restore the
    /// last profile when solo-unlock would, or when that profile has no PIN.
    /// </summary>
    public static bool ShouldRestoreHeadless(ILocalUserService localUsers, bool serverConfigured)
    {
        ArgumentNullException.ThrowIfNull(localUsers);

        if (ShouldRestore(localUsers, serverConfigured))
            return true;

        if (!serverConfigured)
            return false;

        return CanReuseLastRefreshToken(localUsers.GetLastActive(), localUsers);
    }

    /// <summary>
    /// A stored refresh token may be reused without the profile picker when the last
    /// user has no PIN, or the solo profile was already unlocked on this device.
    /// </summary>
    public static bool CanReuseLastRefreshToken(LocalUser? last, ILocalUserService localUsers)
    {
        ArgumentNullException.ThrowIfNull(localUsers);

        if (last is null || string.IsNullOrEmpty(last.RefreshToken))
            return false;

        if (!last.HasPin)
            return true;

        return localUsers.IsSingleUserUnlocked(last.IdentityUserId);
    }

    /// <summary>
    /// Invalid grant / revoked family wipes stored tokens. Transient network failures
    /// must keep the refresh token so Android Auto can retry once the phone has a route.
    /// </summary>
    public static bool ShouldClearStoredTokens(bool invalidGrant) => invalidGrant;

    /// <summary>
    /// Headless playback may re-run restore when a previous attempt finished without an
    /// online access token (no server URL yet, or offline fallback while the car network
    /// was still coming up).
    /// </summary>
    public static bool ShouldRetryHeadlessRestore(
        bool serverConfigured,
        bool restoreAlreadyAttempted,
        bool restoreInFlight,
        bool hasUsableOnlineAccessToken)
    {
        return serverConfigured
            && restoreAlreadyAttempted
            && !restoreInFlight
            && !hasUsableOnlineAccessToken;
    }
}
