using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Commands.QueueRefreshMediaMetadata;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Medias;
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

        var refreshLibraries = await context.Libraries
            .AsNoTracking()
            .Where(l => l.MetadataRefreshIntervalDays != null && l.MetadataRefreshIntervalDays > 0)
            .Select(l => new { l.Id, l.Title, l.MetadataRefreshIntervalDays })
            .ToListAsync(cancellationToken);

        if (refreshLibraries.Count == 0)
            return;

        logger.LogInformation("Checking {Count} libraries for stale metadata", refreshLibraries.Count);

        var now = DateTimeOffset.UtcNow;
        var staleMediaIds = await context.Medias
            .Where(m => m is Movie || m is MusicAlbum || m is Serie || m is MusicArtist)
            .Where(m => context.Libraries.Any(l =>
                l.MetadataRefreshIntervalDays != null
                && l.MetadataRefreshIntervalDays > 0
                && context.MediaLibraryAvailabilities.Any(a =>
                    a.MediaId == m.Id
                    && a.LibraryId == l.Id
                    && (m.LastMetadataRefreshedAt == null
                        || m.LastMetadataRefreshedAt < now.AddDays(-l.MetadataRefreshIntervalDays.Value)))))
            .Select(m => m.Id)
            .Distinct()
            .Take(refreshLibraries.Count * 50)
            .ToListAsync(cancellationToken);

        if (staleMediaIds.Count == 0)
            return;

        logger.LogInformation("Queueing metadata refresh for {Count} stale media", staleMediaIds.Count);

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
