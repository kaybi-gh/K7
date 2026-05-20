using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Web.Middleware;

public class SetupRequiredMiddleware(RequestDelegate next)
{
    private volatile bool _setupCompleted;

    public async Task InvokeAsync(HttpContext context, ISetupService setupService)
    {
        if (_setupCompleted)
        {
            await next(context);
            return;
        }

        if (await setupService.IsSetupCompletedAsync(context.RequestAborted))
        {
            _setupCompleted = true;
            await next(context);
            return;
        }

        if (IsAllowedDuringSetup(context.Request.Path))
        {
            await next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        context.Response.Redirect("/setup");
    }

    private static bool IsAllowedDuringSetup(PathString path)
    {
        return path.StartsWithSegments("/api/setup")
            || path.StartsWithSegments("/api/authentication/callback")
            || path.StartsWithSegments("/health")
            || path.StartsWithSegments("/_framework")
            || path.StartsWithSegments("/_blazor")
            || path.StartsWithSegments("/_content")
            || path.StartsWithSegments("/Account/PerformSetupExternalLogin")
            || path.StartsWithSegments("/setup")
            || path.StartsWithSegments("/css")
            || path.StartsWithSegments("/js")
            || path.StartsWithSegments("/dist");
    }
}

public static class SetupRequiredMiddlewareExtensions
{
    public static IApplicationBuilder UseSetupRequired(this IApplicationBuilder app)
    {
        return app.UseMiddleware<SetupRequiredMiddleware>();
    }
}
