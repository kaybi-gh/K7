using K7.Server.Application.Common;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Endpoints.Admin;

public class GetBackgroundTaskSettings : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet("/api/admin/background-tasks/settings", async (
            [FromServices] BackgroundTasksProcessingService processingService,
            [FromServices] IServerSettingsService settings,
            [FromServices] IApplicationDbContext context,
            [FromServices] MetadataProviderCooldownStore metadataProviderCooldownStore,
            CancellationToken cancellationToken) =>
        {
            var workerCount = await settings.GetAsync(ServerSettingKeys.BackgroundTaskWorkerCount, cancellationToken);
            var limits = await settings.GetAsync(ServerSettingKeys.BackgroundTaskLaneLimits, cancellationToken) ?? new();
            var activeCounts = processingService.ActiveCountByLaneKey;

            var pendingByLane = await context.BackgroundTasks
                .Where(t => t.Status == BackgroundTaskStatus.Pending
                    || t.Status == BackgroundTaskStatus.WaitingForRetry)
                .GroupBy(t => t.Lane)
                .Select(g => new { Lane = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var pendingLookup = pendingByLane.ToDictionary(x => x.Lane, x => x.Count);

            // The lane set is fixed, so always return every lane: an operator must be able to configure
            // a lane before any task has ever used it.
            var lanes = Enum.GetValues<BackgroundTaskLane>()
                .Select(lane => new LaneLimitDto
                {
                    Lane = lane,
                    Limit = limits.GetValueOrDefault(lane, BackgroundTaskScheduling.GetDefaultLimit(lane)),
                    // Federation/Metadata keys carry a suffix, so sum every key belonging to the lane.
                    ActiveCount = activeCounts
                        .Where(kvp => kvp.Key == lane.ToString() || kvp.Key.StartsWith($"{lane}:", StringComparison.Ordinal))
                        .Sum(kvp => kvp.Value),
                    PendingCount = pendingLookup.GetValueOrDefault(lane, 0)
                })
                .ToList();

            var pendingByProvider = await context.BackgroundTasks
                .Where(t => t.Lane == BackgroundTaskLane.Metadata
                    && (t.Status == BackgroundTaskStatus.Pending
                        || t.Status == BackgroundTaskStatus.WaitingForRetry))
                .GroupBy(t => t.MetadataProviderName ?? MetadataProviderNames.Local)
                .Select(g => new { Provider = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            var pendingProviderLookup = pendingByProvider.ToDictionary(
                x => x.Provider,
                x => x.Count,
                StringComparer.OrdinalIgnoreCase);

            var activeCooldowns = metadataProviderCooldownStore.GetActiveCooldowns();

            var metadataProviderNames = MetadataProviderNames.AdmissionKeys
                .Select(provider =>
                {
                    var key = $"Metadata:{provider}";
                    return new MetadataProviderStatsDto
                    {
                        Provider = provider,
                        Limit = BackgroundTaskScheduling.MetadataProviderLimit,
                        ActiveCount = activeCounts.GetValueOrDefault(key),
                        PendingCount = pendingProviderLookup.GetValueOrDefault(provider),
                        CooldownUntil = activeCooldowns.TryGetValue(provider, out var until) ? until : null
                    };
                })
                .ToList();

            return Results.Ok(new BackgroundTaskSettingsDto
            {
                WorkerCount = workerCount,
                Lanes = lanes,
                MetadataProviders = metadataProviderNames
            });
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
