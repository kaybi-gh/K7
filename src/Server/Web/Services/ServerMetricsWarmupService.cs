using K7.Server.Application.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K7.Server.Web.Services;

internal sealed class ServerMetricsWarmupService(
    IServerMetricsCollector metricsCollector,
    ILogger<ServerMetricsWarmupService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        metricsCollector.PrimeCpuBaseline();

        try
        {
            await Task.Delay(500, cancellationToken);
            metricsCollector.RecordSample(0);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to prime server metrics collector");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
