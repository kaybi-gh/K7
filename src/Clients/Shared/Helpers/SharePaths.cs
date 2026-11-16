namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Routes of the public share pages. The landing page reads the link token from the URL
/// fragment. Pages inside a share only carry ids: the visitor session cookie does the rest.
/// </summary>
public static class SharePaths
{
    public const string Landing = "/" + ShareLinkBuilder.PagePath;

    public static string Media(Guid shareId, Guid mediaId) =>
        $"{Landing}/{shareId}/m/{mediaId}";
}
