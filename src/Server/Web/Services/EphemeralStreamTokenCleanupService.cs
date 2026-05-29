using K7.Server.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

internal sealed class EphemeralStreamTokenCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<EphemeralStreamTokenCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                var cutoff = DateTimeOffset.UtcNow;
                var expiredCount = await context.EphemeralStreamTokens
                    .Where(t => t.ExpiresAt < cutoff || t.IsRevoked)
                    .ExecuteDeleteAsync(stoppingToken);

                if (expiredCount > 0)
                {
                    logger.LogInformation("Cleaned up {Count} expired/revoked ephemeral stream tokens", expiredCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error cleaning up ephemeral stream tokens");
            }
        }
    }
}
