using K7.Server.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

public sealed class ScrobbleQueueHostedService(
    IScrobbleQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ScrobbleQueueHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var destinations = scope.ServiceProvider.GetServices<IScrobbleDestination>();
                var destination = destinations.FirstOrDefault(d => d.Provider == item.Provider);
                if (destination is null)
                {
                    logger.LogWarning("No scrobble destination for {Provider}", item.Provider);
                    continue;
                }

                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var account = await context.UserScrobblerAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == item.AccountId, stoppingToken);
                if (account is null)
                    continue;

                // HttpClient already has a capped resilience policy (1 retry, short timeouts).
                // Do not retry again here: that multiplied delays and could starve the queue.
                var sendResult = await destination.SendAsync(
                    account, item.ConfigJson, item.Payload, stoppingToken);
                if (sendResult.Success)
                {
                    logger.LogDebug(
                        "Scrobble sent for account {AccountId} via {Provider}",
                        item.AccountId, item.Provider);
                }
                else
                {
                    logger.LogWarning(
                        "Scrobble skipped for account {AccountId} via {Provider}: {Error}",
                        item.AccountId, item.Provider, sendResult.Error);
                }
            }
            catch (Exception ex)
            {
                // Swallow: scrobble must never affect playback or crash the host.
                logger.LogWarning(
                    ex,
                    "Scrobble abandoned for account {AccountId} via {Provider}",
                    item.AccountId, item.Provider);
            }
        }
    }
}
