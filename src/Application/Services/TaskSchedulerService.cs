using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediaServer.Application.Services;
public class TaskSchedulerService : IHostedService, IDisposable
{
    private readonly ILogger<TaskSchedulerService> _logger;
    private Timer? _timer = null;

    public TaskSchedulerService(ILogger<TaskSchedulerService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Task scheduler service is running.");
        _timer = new Timer(ExecutePendingTasks, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Task scheduler service is stopping.");
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();

    private void ExecutePendingTasks(object? state)
    {

    }
}
