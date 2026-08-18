using K7.Clients.Shared.Interfaces;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Initial BlazorWebView path so MAUI does not mount MainLayout around RedirectToLogin.
/// Solo auto-login still opens home. First-run with no local users opens welcome, or the
/// TV device-link QR when Guest is known to be disabled (welcome would only offer Sign In).
/// </summary>
public static class MauiBlazorStartPath
{
    public const string Home = "/";
    public const string SelectProfile = "/select-profile";
    public const string Welcome = "/welcome";
    public const string LinkDevice = "/linkdevice";

    public static string Resolve(
        ILocalUserService localUsers,
        bool isTv = false,
        bool? guestEnabled = null)
    {
        ArgumentNullException.ThrowIfNull(localUsers);

        if (localUsers.IsSingleUserMode)
        {
            var last = localUsers.GetLastActive();
            if (last is not null && localUsers.IsSingleUserUnlocked(last.IdentityUserId))
                return Home;
        }

        if (localUsers.GetAll().Count > 0)
            return SelectProfile;

        if (isTv && guestEnabled == false)
            return LinkDevice;

        return Welcome;
    }
}
