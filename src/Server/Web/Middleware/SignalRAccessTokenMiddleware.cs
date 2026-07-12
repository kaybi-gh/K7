namespace K7.Server.Web.Middleware;

/// <summary>
/// Copies the SignalR <c>access_token</c> query parameter into the Authorization header
/// so OpenIddict validation can authenticate native clients on the WebSocket handshake.
/// </summary>
public sealed class SignalRAccessTokenMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/hub"))
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
}
