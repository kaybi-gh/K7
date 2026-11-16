using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Helpers;

public static class ShareLinkBuilder
{
    public const string PagePath = "share";

    /// <summary>
    /// Public link of a share. The token sits in the URL fragment: browsers never send a
    /// fragment to the server, so the token stays out of request logs and proxies.
    /// The link points at the server, not at the app shell (a native client has no public URL).
    /// </summary>
    public static string Build(IK7ServerService serverService, string token) =>
        serverService.GetAbsoluteUri($"{PagePath}#{token}")?.AbsoluteUri ?? $"/{PagePath}#{token}";
}
