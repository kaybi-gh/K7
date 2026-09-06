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
            return uri;

        if (uri.StartsWith("k7://", StringComparison.OrdinalIgnoreCase)
            || IsLoopbackCallback(uri))
        {
            return uri;
        }

        if (IsLocalPath(uri) || Uri.IsWellFormedUriString(uri, UriKind.Relative))
            return EncodeQuery(uri);

        return toBaseRelativePath is not null ? toBaseRelativePath(uri) : uri;
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

    private static string EncodeQueryToken(string token)
    {
        var decoded = Uri.UnescapeDataString(token.Replace('+', ' '));
        return Uri.EscapeDataString(decoded);
    }
}
