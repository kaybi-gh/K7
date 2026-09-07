namespace K7.Server.Web.Middleware;

/// <summary>
/// Antiforgery rejection on static auth forms is an empty HTTP 400. Chromium shows
/// "HTTP ERROR 400" and that navigation can abort the OIDC loopback redirect the
/// Windows client is waiting on. Convert those empty 400s to a GET of the same form.
/// </summary>
public sealed class AuthFormBadRequestRecoveryMiddleware(RequestDelegate next, ILogger<AuthFormBadRequestRecoveryMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (!ShouldRecover(context))
            return;

        var location = context.Request.Path + context.Request.QueryString;
        logger.LogWarning(
            "Empty 400 on POST {Path}; redirecting to GET (likely antiforgery or a duplicate submit)",
            context.Request.Path.Value);

        context.Response.Clear();
        context.Response.Redirect(location);
    }

    internal static bool ShouldRecover(HttpContext context)
    {
        if (context.Response.HasStarted)
            return false;

        if (context.Response.StatusCode != StatusCodes.Status400BadRequest)
            return false;

        if (!HttpMethods.IsPost(context.Request.Method))
            return false;

        if (!IsAuthFormPath(context.Request.Path))
            return false;

        var contentType = context.Response.ContentType;
        if (!string.IsNullOrEmpty(contentType)
            && !contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase)
            && !contentType.StartsWith("text/plain", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    internal static bool IsAuthFormPath(PathString path) =>
        path.StartsWithSegments("/sign-in")
        || path.StartsWithSegments("/sign-up")
        || path.StartsWithSegments("/setup")
        || path.StartsWithSegments("/account");
}
