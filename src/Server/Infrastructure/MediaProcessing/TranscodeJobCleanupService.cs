using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing;

public class TranscodeJobCleanupService : BackgroundService
{
    private readonly ITranscodeJobManager _jobManager;
    private readonly ILogger<TranscodeJobCleanupService> _logger;
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _staleThreshold = TimeSpan.FromHours(1);

    public TranscodeJobCleanupService(
        ITranscodeJobManager jobManager,
        ILogger<TranscodeJobCleanupService> logger)
    {
        _jobManager = jobManager;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Transcode job cleanup service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_cleanupInterval, stoppingToken);
                
                _logger.LogDebug("Running transcode job cleanup");
                await _jobManager.CleanupStaleJobsAsync(_staleThreshold, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when service is stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during transcode job cleanup");
            }
        }

        _logger.LogInformation("Transcode job cleanup service stopped");
    }
}
