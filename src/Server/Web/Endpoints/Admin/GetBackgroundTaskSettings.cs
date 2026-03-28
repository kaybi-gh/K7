using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
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
            CancellationToken cancellationToken) =>
        {
            var workerCount = await settings.GetAsync(ServerSettingKeys.BackgroundTaskWorkerCount, cancellationToken);
            var limits = await settings.GetAsync(ServerSettingKeys.BackgroundTaskConcurrencyLimits, cancellationToken) ?? new();
            var activeCounts = processingService.ActiveCountByGroup;

            var knownGroups = await context.BackgroundTasks
                .Where(t => t.ConcurrencyGroup != null)
                .Select(t => (string)t.ConcurrencyGroup!)
                .Distinct()
                .ToListAsync(cancellationToken);

            var allGroupNames = knownGroups
                .Union(limits.Keys)
                .Union(activeCounts.Keys)
                .Distinct()
                .Order()
                .ToList();

            var groups = allGroupNames.Select(name => new ConcurrencyGroupDto
            {
                Name = name,
                Limit = limits.GetValueOrDefault(name, 1),
                ActiveCount = activeCounts.GetValueOrDefault(name, 0)
            }).ToList();

            return Results.Ok(new BackgroundTaskSettingsDto
            {
                WorkerCount = workerCount,
                ConcurrencyGroups = groups
            });
        })
        .RequireAuthorization(Policies.AdminOnly)
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
