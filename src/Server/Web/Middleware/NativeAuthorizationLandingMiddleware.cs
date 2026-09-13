using K7.Server.Web.Components.Account;

namespace K7.Server.Web.Middleware;

/// <summary>
/// After a Windows system-browser authorize, replace Location k7://... with
/// /auth/complete so the tab shows the same close message as TV device login.
/// The complete page then opens k7:// so the running app still receives the code.
/// </summary>
public sealed class NativeAuthorizationLandingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        await next(context);

        if (context.Response.HasStarted)
            return;

        if (context.Request.Path.StartsWithSegments("/connect/authorize")
            && IdentityRedirectUri.WantsBrowserLanding(context.Request))
        {
            var rewritten = IdentityRedirectUri.RewriteNativeCallbackToLanding(
                context.Response.Headers.Location.ToString());
            if (rewritten is not null)
                context.Response.Headers.Location = rewritten;
            return;
        }

        if (!context.Request.Path.StartsWithSegments("/auth/complete"))
            return;

        var launch = context.Request.Query["launch"].ToString();
        if (IdentityRedirectUri.TryGetNativeCallback(launch, out var callback))
            context.Response.Headers["Refresh"] = IdentityRedirectUri.BuildNativeLaunchRefresh(callback);
    }
}

public static class NativeAuthorizationLandingMiddlewareExtensions
{
    public static IApplicationBuilder UseNativeAuthorizationLanding(this IApplicationBuilder app) =>
        app.UseMiddleware<NativeAuthorizationLandingMiddleware>();
}
