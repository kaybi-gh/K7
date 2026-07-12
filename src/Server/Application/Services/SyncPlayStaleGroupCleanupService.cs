using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;

public class SyncPlayStaleGroupCleanupService : BackgroundService
{
    private readonly ISyncPlayCoordinator _syncPlayCoordinator;
    private readonly ILogger<SyncPlayStaleGroupCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(5);

    public SyncPlayStaleGroupCleanupService(
        ISyncPlayCoordinator syncPlayCoordinator,
        ILogger<SyncPlayStaleGroupCleanupService> logger)
    {
        _syncPlayCoordinator = syncPlayCoordinator;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncPlay stale group cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                _logger.LogDebug("Running SyncPlay stale group cleanup");
                _syncPlayCoordinator.CleanupStaleGroups();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SyncPlay stale group cleanup");
            }
        }

        _logger.LogInformation("SyncPlay stale group cleanup service stopped");
    }
}
