using Microsoft.AspNetCore.Http;

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
    /// <summary>
    /// Form first, then a correctly named query binding, then the request query.
    /// <c>[SupplyParameterFromQuery] ReturnUrlQuery</c> does not bind <c>?ReturnUrl=</c>.
    /// An empty value after password login sends the browser to web home instead of
    /// <c>/connect/authorize</c>, so the native app never gets the callback.
    /// </summary>
    public static string? Coalesce(string? formValue, string? queryBoundValue, IQueryCollection? query)
    {
        if (!string.IsNullOrEmpty(formValue))
            return formValue;

        if (!string.IsNullOrEmpty(queryBoundValue))
            return queryBoundValue;

        if (query is null)
            return null;

        if (query.TryGetValue("ReturnUrl", out var returnUrl) && !string.IsNullOrEmpty(returnUrl))
            return returnUrl.ToString();

        if (query.TryGetValue("returnUrl", out returnUrl) && !string.IsNullOrEmpty(returnUrl))
            return returnUrl.ToString();

        return null;
    }

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

    internal static bool IsNativeCallback(string? uri) =>
        TryGetNativeCallback(uri, out _);

    internal static bool TryGetNativeCallback(string? uri, out Uri callback)
    {
        callback = null!;
        if (!Uri.TryCreate(uri, UriKind.Absolute, out var parsed)
            || !string.Equals(parsed.Scheme, "k7", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(parsed.Host, "callback", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!string.Equals(parsed.AbsolutePath, "/login", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(parsed.AbsolutePath, "/logout", StringComparison.OrdinalIgnoreCase))
            return false;

        callback = parsed;
        return true;
    }

    /// <summary>
    /// System-browser native login (Windows) needs an HTTP page after authorize.
    /// A raw 302 to <c>k7://</c> leaves the tab on /sign-in. Mobile in-app browsers
    /// must keep the custom-scheme Location so they can finish the session.
    /// </summary>
    internal static bool WantsBrowserLanding(HttpRequest request)
    {
        if (HasDisplayPage(request))
            return true;

        var userAgent = request.Headers.UserAgent.ToString();
        return userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase);
    }

    internal static string? RewriteNativeCallbackToLanding(string? location)
    {
        if (!TryGetNativeCallback(location, out var callback))
            return null;

        var query = callback.Query;
        var status = query.Contains("error=access_denied", StringComparison.OrdinalIgnoreCase)
            ? "denied"
            : query.Contains("error=", StringComparison.OrdinalIgnoreCase)
                ? "error"
                : "success";

        return "/auth/complete?status=" + status + "&launch=" + Uri.EscapeDataString(callback.AbsoluteUri);
    }

    internal static string BuildNativeLaunchRefresh(Uri callback) =>
        "0; url=" + callback.AbsoluteUri;

    private static bool HasDisplayPage(HttpRequest request)
    {
        if (request.Query.TryGetValue("display", out var queryDisplay)
            && string.Equals(queryDisplay.ToString(), "page", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!request.HasFormContentType)
            return false;

        try
        {
            return request.Form.TryGetValue("display", out var formDisplay)
                && string.Equals(formDisplay.ToString(), "page", StringComparison.OrdinalIgnoreCase);
        }
        catch (InvalidOperationException)
        {
            return false;
        }
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
