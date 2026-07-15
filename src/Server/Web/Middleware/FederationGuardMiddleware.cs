using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Settings;
using K7.Shared.Dtos;
using Microsoft.Extensions.Caching.Memory;

namespace K7.Server.Web.Middleware;

public class FederationGuardMiddleware(RequestDelegate next)
{
    private const string FeatureFlagsCacheKey = "server:feature-flags";
    private static readonly TimeSpan FeatureFlagsCacheDuration = TimeSpan.FromMinutes(1);

    public async Task InvokeAsync(
        HttpContext context,
        IServerSettingsService serverSettingsService,
        IMemoryCache memoryCache)
    {
        var path = context.Request.Path.Value;

        if (path is null || !path.StartsWith("/api/federation/", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var flags = await memoryCache.GetOrCreateAsync(FeatureFlagsCacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = FeatureFlagsCacheDuration;
            return await serverSettingsService.GetFeatureFlagsAsync(context.RequestAborted);
        }) ?? new ServerFeatureFlagsDto();

        if (!flags.FederationEnabled)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsJsonAsync(new { Title = "Federation is disabled on this server." });
            return;
        }

        if (!flags.FederationInvitationsEnabled)
        {
            var isInvitationRoute = path.Equals("/api/federation/peer-request", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/federation/peers/request", StringComparison.OrdinalIgnoreCase);

            if (isInvitationRoute)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { Title = "Federation invitations are disabled on this server." });
                return;
            }
        }

        await next(context);
    }
}
