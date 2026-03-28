using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Admin;

public class UpdateBackgroundTaskSettings : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPut("/api/admin/background-tasks/settings", async (
            [FromBody] UpdateBackgroundTaskSettingsRequest request,
            [FromServices] IServerSettingsService settings,
            CancellationToken cancellationToken) =>
        {
            if (request.WorkerCount.HasValue)
            {
                var count = Math.Clamp(request.WorkerCount.Value, 1, 32);
                await settings.SetAsync(ServerSettingKeys.BackgroundTaskWorkerCount, count, cancellationToken);
            }

            if (request.ConcurrencyLimits is not null)
            {
                var sanitized = request.ConcurrencyLimits
                    .Where(kvp => !string.IsNullOrWhiteSpace(kvp.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => Math.Max(0, kvp.Value));
                await settings.SetAsync(ServerSettingKeys.BackgroundTaskConcurrencyLimits, sanitized, cancellationToken);
            }

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
