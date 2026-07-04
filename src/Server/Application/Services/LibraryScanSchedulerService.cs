using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Libraries.Commands.IndexLibraryFiles;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;

public sealed class LibraryScanSchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<LibraryScanSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("LibraryScanSchedulerService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
                await ProcessDueScansAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in library scan scheduler");
            }
        }

        logger.LogInformation("LibraryScanSchedulerService stopped");
    }

    private async Task ProcessDueScansAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var libraries = await context.Libraries
            .AsNoTracking()
            .Where(l => l.RootPath != null && l.PeerServerId == null && l.AutoScanIntervalHours > 0)
            .ToListAsync(cancellationToken);

        if (libraries.Count == 0)
            return;

        var libraryIds = libraries.Select(l => l.Id).ToList();
        var lastCompletedScans = await context.BackgroundTasks
            .Where(t => t.TargetEntityId != null
                && libraryIds.Contains(t.TargetEntityId.Value)
                && t.Name == nameof(IndexLibraryFilesCommand)
                && t.Status == BackgroundTaskStatus.Completed
                && t.CompletedAt != null)
            .GroupBy(t => t.TargetEntityId!.Value)
            .Select(g => new { LibraryId = g.Key, CompletedAt = g.Max(t => t.CompletedAt) })
            .ToListAsync(cancellationToken);

        var lastScanMap = lastCompletedScans.ToDictionary(x => x.LibraryId, x => x.CompletedAt);
        var now = DateTimeOffset.UtcNow;

        foreach (var library in libraries)
        {
            var interval = TimeSpan.FromHours(library.AutoScanIntervalHours);
            if (lastScanMap.TryGetValue(library.Id, out var lastCompletedAt)
                && lastCompletedAt.HasValue
                && now - lastCompletedAt.Value < interval)
            {
                continue;
            }

            logger.LogInformation("Queueing periodic scan for library {LibraryId} ({Title})", library.Id, library.Title);

            await sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new IndexLibraryFilesCommand(library.Id),
                Priority = BackgroundTaskPriority.Low,
                TargetEntityId = library.Id,
                TargetEntityTypeName = nameof(Library),
                MaxAttempts = 1,
                TimeoutSeconds = 3600,
                ConcurrencyGroup = "library-scan"
            }, cancellationToken);
        }
    }
}
