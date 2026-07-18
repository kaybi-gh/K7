using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

internal sealed class StreamSessionCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<StreamSessionCleanupService> logger) : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan SoftEndAfter = TimeSpan.FromDays(2);
    private static readonly TimeSpan DeleteEndedAfter = TimeSpan.FromDays(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CleanupInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
                var now = DateTimeOffset.UtcNow;

                var softEndCutoff = now - SoftEndAfter;
                var softEnded = await context.StreamSessions
                    .Where(s => s.EndedAt == null && s.LastModified < softEndCutoff)
                    .ExecuteUpdateAsync(
                        s => s
                            .SetProperty(x => x.EndedAt, now)
                            .SetProperty(x => x.State, PlaybackState.Ended),
                        stoppingToken);

                var deleteCutoff = now - DeleteEndedAfter;
                var deleted = await context.StreamSessions
                    .Where(s => s.EndedAt != null && s.EndedAt < deleteCutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                if (softEnded > 0 || deleted > 0)
                {
                    logger.LogInformation(
                        "Stream session cleanup soft-ended {SoftEnded} and deleted {Deleted} sessions",
                        softEnded,
                        deleted);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error cleaning up stream sessions");
            }
        }
    }
}
