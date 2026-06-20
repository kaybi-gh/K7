using K7.Server.Application.Services;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

internal sealed class AdminMetricsNotifier(
    IHubContext<K7Hub, IK7HubClient> hubContext,
    IServerMetricsCollector metricsCollector,
    ILogger<AdminMetricsNotifier> logger) : BackgroundService
{
    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(BroadcastInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                metricsCollector.RecordSample(0);
                var snapshots = metricsCollector.GetHistory().Snapshots;
                if (snapshots.Count == 0)
                    continue;

                var snapshot = snapshots[^1];
                await hubContext.Clients.Group(K7Hub.AdminStreamsGroup)
                    .ReceiveServerMetricsUpdated(snapshot);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to broadcast server metrics to admin group");
            }
        }
    }
}
