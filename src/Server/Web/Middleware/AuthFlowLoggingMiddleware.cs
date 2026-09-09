using K7.Server.Web.Components.Account;

namespace K7.Server.Web.Middleware;

/// <summary>
/// Writes a single structured line per auth-related request so docker logs show
/// whether ReturnUrl survived, where the 302 pointed, and which OIDC client/grant
/// ran. Values are classifications only (no codes, tokens, or full URLs).
/// </summary>
public sealed class AuthFlowLoggingMiddleware(RequestDelegate next, ILogger<AuthFlowLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (!IsAuthPath(context.Request.Path))
            return;

        var returnUrl = ReadReturnUrl(context.Request);
        var location = context.Response.Headers.Location.ToString();
        var redirectUri = ReadValue(context.Request, "redirect_uri");
        var clientId = ReadValue(context.Request, "client_id");
        var grantType = ReadValue(context.Request, "grant_type");

        logger.LogInformation(
            "Auth {Method} {Path} {StatusCode} cookie {CookieAuth} returnUrl {ReturnUrlKind} location {LocationKind} client {ClientId} grant {GrantType} redirectHost {RedirectHost} locationHost {LocationHost}",
            context.Request.Method,
            context.Request.Path.Value ?? "/",
            context.Response.StatusCode,
            context.User.Identity?.IsAuthenticated == true,
            IdentityRedirectUri.Classify(returnUrl),
            IdentityRedirectUri.Classify(string.IsNullOrEmpty(location) ? null : location),
            string.IsNullOrEmpty(clientId) ? "-" : clientId,
            string.IsNullOrEmpty(grantType) ? "-" : grantType,
            IdentityRedirectUri.DescribeHost(redirectUri),
            IdentityRedirectUri.DescribeHost(string.IsNullOrEmpty(location) ? null : location));
    }

    internal static bool IsAuthPath(PathString path) =>
        path.StartsWithSegments("/sign-in")
        || path.StartsWithSegments("/welcome")
        || path.StartsWithSegments("/connect/authorize")
        || path.StartsWithSegments("/connect/token")
        || path.StartsWithSegments("/auth/complete");

    internal static string ReadReturnUrl(HttpRequest request)
    {
        var formValue = ReadFormValue(request, "ReturnUrl");
        if (string.IsNullOrEmpty(formValue))
            formValue = ReadFormValue(request, "returnUrl");
        if (!string.IsNullOrEmpty(formValue))
            return formValue;

        var queryValue = ReadQueryValue(request, "ReturnUrl");
        if (string.IsNullOrEmpty(queryValue))
            queryValue = ReadQueryValue(request, "returnUrl");
        return queryValue;
    }

    internal static string ReadValue(HttpRequest request, string key)
    {
        var queryValue = ReadQueryValue(request, key);
        if (!string.IsNullOrEmpty(queryValue))
            return queryValue;

        return ReadFormValue(request, key);
    }

    private static string ReadQueryValue(HttpRequest request, string key)
    {
        if (request.Query.TryGetValue(key, out var queryValue))
            return queryValue.ToString();

        return "";
    }

    private static string ReadFormValue(HttpRequest request, string key)
    {
        if (!request.HasFormContentType)
            return "";

        try
        {
            if (request.Form.TryGetValue(key, out var formValue))
                return formValue.ToString();
        }
        catch (InvalidOperationException)
        {
        }

        return "";
    }
}

public static class AuthFlowLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthFlowLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<AuthFlowLoggingMiddleware>();
}
