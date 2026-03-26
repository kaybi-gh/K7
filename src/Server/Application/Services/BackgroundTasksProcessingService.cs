using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;
public class BackgroundTasksProcessingService : BackgroundService
{
    private readonly ILogger<BackgroundTasksProcessingService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public BackgroundTasksProcessingService(
        ILogger<BackgroundTasksProcessingService> logger,
        IServiceProvider serviceProvider
    )
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("BackgroundTasksProcessingService: Starting.");

        while (!cancellationToken.IsCancellationRequested)
        {
            using var backgroundServiceExecutionScope = _serviceProvider.CreateScope();
            var context = backgroundServiceExecutionScope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

            var task = await context.BackgroundTasks
                .Where(t => t.Status == BackgroundTaskStatus.Pending)
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => x.Created)
                .FirstOrDefaultAsync(cancellationToken);
            
            if (task != null)
            {
                await ExecuteBackgroundTaskAsync(task, context, cancellationToken);
            }
            else
            {
                await Task.Delay(5000, cancellationToken);
            }
        }

        _logger.LogInformation("Persistent Task Processing Service is stopping.");
    }

    private async Task ExecuteBackgroundTaskAsync(
        BackgroundTask task,
        IApplicationDbContext context,
        CancellationToken cancellationToken
    )
    {
        using var taskScope = _serviceProvider.CreateScope();
        var sender = taskScope.ServiceProvider.GetRequiredService<ISender>();

        try
        {
            task.Status = BackgroundTaskStatus.InProgress;
            await context.SaveChangesAsync(cancellationToken);

            var requestType = Type.GetType(task.RequestType)
                ?? throw new Exception("Deserialization failed.");
            var request = JsonSerializer.Deserialize(task.RequestData, requestType)
                ?? throw new Exception("Deserialization failed.");

            await sender.Send(request, cancellationToken);
            task.Status = BackgroundTaskStatus.Completed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing task {TaskId}", task.Id);

            task.RetryCount++;
            task.Status = BackgroundTaskStatus.Pending;

            if (task.RetryCount >= task.MaxRetryCount)
            {
                _logger.LogError("Task exceeded max retries, removing it from queue.");
                task.Status = BackgroundTaskStatus.Failed;
            }
            else
            {
                _logger.LogWarning(ex, "Task {TaskId} failed, retrying ({RetryCount}/{MaxRetryCount})", task.Id, task.RetryCount, task.MaxRetryCount);
            }
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
