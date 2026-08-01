using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
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
            [FromServices] BackgroundTasksProcessingService processingService,
            CancellationToken cancellationToken) =>
        {
            if (request.WorkerCount.HasValue)
            {
                var count = Math.Clamp(
                    request.WorkerCount.Value,
                    1,
                    BackgroundTaskScheduling.MaxWorkerCount);
                await settings.SetAsync(ServerSettingKeys.BackgroundTaskWorkerCount, count, cancellationToken);
            }

            if (request.LaneLimits is not null)
            {
                // Unknown enum values are dropped rather than persisted: the lane set is fixed and a
                // stale client must not be able to write a limit nothing will ever read.
                var sanitized = request.LaneLimits
                    .Where(kvp => Enum.IsDefined(kvp.Key))
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => Math.Clamp(kvp.Value, 0, BackgroundTaskScheduling.MaxLaneLimit));
                await settings.SetAsync(ServerSettingKeys.BackgroundTaskLaneLimits, sanitized, cancellationToken);
            }

            await processingService.ApplySettingsAsync(cancellationToken);

            return Results.NoContent();
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
