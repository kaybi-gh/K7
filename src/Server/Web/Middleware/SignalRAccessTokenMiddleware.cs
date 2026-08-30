namespace K7.Server.Web.Middleware;

/// <summary>
/// Copies the <c>access_token</c> query parameter into the Authorization header so
/// OpenIddict can authenticate clients that cannot send headers (SignalR handshake,
/// LibVLC Android HTTP).
/// </summary>
public sealed class SignalRAccessTokenMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (ShouldCopyAccessToken(context.Request.Path))
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken) &&
                !context.Request.Headers.ContainsKey("Authorization"))
            {
                context.Request.Headers.Authorization = $"Bearer {accessToken}";
            }
        }

        return next(context);
    }

    private static bool ShouldCopyAccessToken(PathString path) =>
        path.StartsWithSegments("/hub")
        || path.StartsWithSegments("/api/indexed-files")
        || path.StartsWithSegments("/api/remote-stream-sessions");
}
