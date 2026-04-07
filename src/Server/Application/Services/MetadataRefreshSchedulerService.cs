using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Commands.QueueRefreshMediaMetadata;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;

public class MetadataRefreshSchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<MetadataRefreshSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("MetadataRefreshSchedulerService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
                await ProcessStaleMediaAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in metadata refresh scheduler");
            }
        }

        logger.LogInformation("MetadataRefreshSchedulerService stopped");
    }

    private async Task ProcessStaleMediaAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var libraries = await context.Libraries
            .Where(l => l.MetadataRefreshIntervalDays != null && l.MetadataRefreshIntervalDays > 0)
            .ToListAsync(cancellationToken);

        if (libraries.Count == 0)
            return;

        logger.LogInformation("Checking {Count} libraries for stale metadata", libraries.Count);

        foreach (var library in libraries)
        {
            var threshold = DateTimeOffset.UtcNow.AddDays(-library.MetadataRefreshIntervalDays!.Value);

            var staleMediaIds = await context.Medias
                .Where(m => m.IndexedFiles.Any(f => f.LibraryId == library.Id))
                .Where(m => m.LastMetadataRefreshedAt == null || m.LastMetadataRefreshedAt < threshold)
                .Select(m => m.Id)
                .Take(50)
                .ToListAsync(cancellationToken);

            if (staleMediaIds.Count == 0)
                continue;

            logger.LogInformation("Library '{Title}': queueing metadata refresh for {Count} stale media", library.Title, staleMediaIds.Count);

            foreach (var mediaId in staleMediaIds)
            {
                try
                {
                    await sender.Send(new QueueRefreshMediaMetadataCommand { MediaId = mediaId }, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to queue metadata refresh for media {MediaId}", mediaId);
                }
            }
        }
    }
}
