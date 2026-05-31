using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Federation.Commands.SyncPeerMetadata;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;

public class PeerSyncSchedulerService(
    IServiceScopeFactory scopeFactory,
    ILogger<PeerSyncSchedulerService> logger) : BackgroundService
{
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PeerSyncSchedulerService started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(SyncInterval, stoppingToken);
                await SyncAllPeersAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in peer sync scheduler");
            }
        }

        logger.LogInformation("PeerSyncSchedulerService stopped");
    }

    private async Task SyncAllPeersAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var activePeerIds = await context.PeerServers
            .Where(p => p.Status == PeerStatus.Active
                && p.OutboundClientId != null
                && p.OutboundClientSecret != null)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        if (activePeerIds.Count == 0)
            return;

        logger.LogInformation("Syncing metadata from {Count} active peers", activePeerIds.Count);

        foreach (var peerId in activePeerIds)
        {
            try
            {
                await sender.Send(new SyncPeerMetadataCommand(peerId), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to sync peer {PeerId}", peerId);
            }
        }
    }
}
