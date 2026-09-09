namespace K7.Server.Web.Components.Account;

/// <summary>
/// Normalizes Identity post-login destinations. Query-bound ReturnUrl values are URL-decoded,
/// so OIDC authorize URLs contain spaces in <c>scope</c> and fail
/// <see cref="Uri.IsWellFormedUriString(string?, UriKind)"/>. Sending those through
/// <c>NavigationManager.ToBaseRelativePath</c> throws and leaves the browser on /sign-in
/// while native clients wait for the loopback callback.
/// Re-encoding the query also avoids WAF rules (Nginx Proxy Manager Block Common Exploits)
/// that 403 any raw <c>param=http://</c> such as <c>redirect_uri=http://localhost:port/</c>.
/// </summary>
internal static class IdentityRedirectUri
{
    public static string Normalize(string? uri, Func<string, string>? toBaseRelativePath = null)
    {
        uri ??= "";
        if (uri.Length == 0)
            return "/";

        if (uri.StartsWith("k7://", StringComparison.OrdinalIgnoreCase)
            || IsLoopbackCallback(uri))
        {
            return uri;
        }

        if (IsLocalPath(uri) || Uri.IsWellFormedUriString(uri, UriKind.Relative))
            return EncodeQuery(uri);

        return toBaseRelativePath is not null ? toBaseRelativePath(uri) : uri;
    }

    /// <summary>
    /// Safe label for logs. Never includes query strings, codes, or tokens.
    /// </summary>
    internal static string Classify(string? uri)
    {
        if (string.IsNullOrEmpty(uri))
            return "empty";

        if (uri.StartsWith("k7://", StringComparison.OrdinalIgnoreCase))
            return "custom-scheme";

        if (IsLoopbackCallback(uri))
            return "loopback";

        if (IsLocalPath(uri) || Uri.IsWellFormedUriString(uri, UriKind.Relative))
            return ClassifyPath(uri);

        if (Uri.TryCreate(uri, UriKind.Absolute, out var absolute)
            && absolute.Scheme is "http" or "https")
            return ClassifyPath(absolute.PathAndQuery);

        return "other";
    }

    internal static string DescribeHost(string? uri)
    {
        if (string.IsNullOrEmpty(uri)
            || !Uri.TryCreate(uri, UriKind.Absolute, out var absolute)
            || absolute.Scheme is not ("http" or "https")
            || string.IsNullOrEmpty(absolute.Host))
            return "-";

        return absolute.IsDefaultPort ? absolute.Host : $"{absolute.Host}:{absolute.Port}";
    }

    internal static bool IsLocalPath(string uri) =>
        uri.StartsWith('/') && !uri.StartsWith("//", StringComparison.Ordinal);

    internal static bool IsLoopbackCallback(string uri)
    {
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var absolute))
            return false;

        return absolute.Scheme is "http" or "https"
            && (absolute.IsLoopback
                || string.Equals(absolute.Host, "localhost", StringComparison.OrdinalIgnoreCase));
    }

    internal static string EncodeQuery(string uri)
    {
        var queryIndex = uri.IndexOf('?');
        if (queryIndex < 0)
            return uri;

        var path = uri[..queryIndex];
        var query = uri[(queryIndex + 1)..];
        if (query.Length == 0)
            return uri;

        var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        var encoded = new string[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var separator = part.IndexOf('=');
            if (separator < 0)
            {
                encoded[i] = EncodeQueryToken(part);
                continue;
            }

            encoded[i] = EncodeQueryToken(part[..separator]) + "=" + EncodeQueryToken(part[(separator + 1)..]);
        }

        return path + "?" + string.Join("&", encoded);
    }

    private static string ClassifyPath(string uri)
    {
        var path = uri;
        var queryIndex = uri.IndexOf('?');
        if (queryIndex >= 0)
            path = uri[..queryIndex];

        if (path.Length == 0 || path == "/")
            return "home";
        if (path.StartsWith("/connect/authorize", StringComparison.OrdinalIgnoreCase))
            return "local-authorize";
        if (path.StartsWith("/sign-in", StringComparison.OrdinalIgnoreCase))
            return "sign-in";
        if (path.StartsWith("/welcome", StringComparison.OrdinalIgnoreCase))
            return "welcome";
        if (path.StartsWith("/auth/complete", StringComparison.OrdinalIgnoreCase))
            return "auth-complete";

        return "local-other";
    }

    private static string EncodeQueryToken(string token)
    {
        var decoded = Uri.UnescapeDataString(token.Replace('+', ' '));
        return Uri.EscapeDataString(decoded);
    }
}
