using K7.Server.Application.Services;
using K7.Server.Web.Endpoints.Hubs;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

internal sealed class AdminStreamNotifier(
    IHubContext<K7Hub, IK7HubClient> hubContext,
    IServiceScopeFactory scopeFactory,
    ILogger<AdminStreamNotifier> logger) : BackgroundService
{
    private static readonly TimeSpan BroadcastInterval = TimeSpan.FromSeconds(10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(BroadcastInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var snapshotService = scope.ServiceProvider.GetRequiredService<IActiveStreamsSnapshotService>();
                var streams = await snapshotService.BuildAsync(stoppingToken);

                await hubContext.Clients.Group(K7Hub.AdminStreamsGroup)
                    .ReceiveActiveStreamsUpdated(streams);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to broadcast active streams to admin group");
            }
        }
    }
}
