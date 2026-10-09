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
    IAdminStreamAudience audience,
    ILogger<AdminStreamNotifier> logger) : BackgroundService
{
    private static readonly TimeSpan FastInterval = TimeSpan.FromSeconds(2);
    private const int TvTickEvery = 5;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(FastInterval);
        var tick = 0;

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                tick++;
                using var scope = scopeFactory.CreateScope();
                var snapshotService = scope.ServiceProvider.GetRequiredService<IActiveStreamsSnapshotService>();
                var streams = await snapshotService.BuildAsync(stoppingToken);
                var tvIds = audience.TvConnectionIds;

                await hubContext.Clients.GroupExcept(K7Hub.AdminStreamsGroup, tvIds)
                    .ReceiveActiveStreamsUpdated(streams);

                if (tick % TvTickEvery == 0 && tvIds.Count > 0)
                {
                    await hubContext.Clients.Clients(tvIds)
                        .ReceiveActiveStreamsUpdated(streams);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to broadcast active streams to admin group");
            }
        }
    }
}
