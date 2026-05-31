using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Web.Middleware;

public class FederationGuardMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IServerSettingsService serverSettingsService)
    {
        var path = context.Request.Path.Value;

        if (path is null || !path.StartsWith("/api/federation/", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var flags = await GetFeatureFlagsAsync(serverSettingsService, context.RequestAborted);

        if (!flags.FederationEnabled)
        {
            context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
            await context.Response.WriteAsJsonAsync(new { Title = "Federation is disabled on this server." });
            return;
        }

        if (!flags.FederationInvitationsEnabled)
        {
            var isInvitationRoute = path.Equals("/api/federation/peer-request", StringComparison.OrdinalIgnoreCase)
                || path.Equals("/api/federation/peers/request", StringComparison.OrdinalIgnoreCase);

            if (isInvitationRoute)
            {
                context.Response.StatusCode = StatusCodes.Status405MethodNotAllowed;
                await context.Response.WriteAsJsonAsync(new { Title = "Federation invitations are disabled on this server." });
                return;
            }
        }

        await next(context);
    }

    private static async Task<ServerFeatureFlagsDto> GetFeatureFlagsAsync(IServerSettingsService serverSettingsService, CancellationToken cancellationToken)
    {
        var json = await serverSettingsService.GetAsync(ServerSettingKeys.FeatureFlags, cancellationToken);
        if (json is not null)
            return JsonSerializer.Deserialize<ServerFeatureFlagsDto>(json) ?? new ServerFeatureFlagsDto();

        return new ServerFeatureFlagsDto();
    }
}
