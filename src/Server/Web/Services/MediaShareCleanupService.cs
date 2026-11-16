using K7.Server.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Web.Services;

/// <summary>
/// Drops visitor sessions that expired a while ago, with their technical devices.
/// Sessions are kept some time after expiring so share owners can still see who came.
/// </summary>
internal sealed class MediaShareCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<MediaShareCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan KeepExpiredSessionsFor = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var cutoff = DateTimeOffset.UtcNow - KeepExpiredSessionsFor;

                var deviceIds = await context.MediaShareSessions
                    .Where(s => s.ExpiresAt < cutoff && s.DeviceId != null)
                    .Select(s => s.DeviceId!.Value)
                    .ToListAsync(stoppingToken);

                var deleted = await context.MediaShareSessions
                    .Where(s => s.ExpiresAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deviceIds.Count > 0)
                {
                    await context.Devices
                        .Where(d => deviceIds.Contains(d.Id))
                        .ExecuteDeleteAsync(stoppingToken);
                }

                if (deleted > 0)
                    logger.LogInformation("Cleaned up {Count} expired share visitor sessions", deleted);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error cleaning up share visitor sessions");
            }
        }
    }
}
