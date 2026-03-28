using System.Diagnostics;
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
    private const int WorkerCount = 3;
    private static readonly TimeSpan OrphanPollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan CompletedRetention = TimeSpan.FromDays(7);
    private static readonly TimeSpan FailedRetention = TimeSpan.FromDays(30);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(15);

    private readonly ILogger<BackgroundTasksProcessingService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly BackgroundTaskTypeRegistry _typeRegistry;

    public BackgroundTasksProcessingService(
        ILogger<BackgroundTasksProcessingService> logger,
        IServiceProvider serviceProvider,
        IBackgroundTaskQueue taskQueue,
        BackgroundTaskTypeRegistry typeRegistry)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _taskQueue = taskQueue;
        _typeRegistry = typeRegistry;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BackgroundTasksProcessingService starting with {WorkerCount} workers", WorkerCount);

        await RecoverStuckTasksAsync(stoppingToken);
        await RequeueEligibleTasksAsync(stoppingToken);

        var workers = Enumerable.Range(0, WorkerCount)
            .Select(i => RunWorkerAsync(i, stoppingToken))
            .ToList();

        workers.Add(RunOrphanPollerAsync(stoppingToken));
        workers.Add(RunCleanupAsync(stoppingToken));

        await Task.WhenAll(workers);

        _logger.LogInformation("BackgroundTasksProcessingService stopped");
    }

    private async Task RecoverStuckTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var stuckTasks = await context.BackgroundTasks
            .Where(t => t.Status == BackgroundTaskStatus.InProgress)
            .ToListAsync(cancellationToken);

        foreach (var task in stuckTasks)
        {
            task.Status = BackgroundTaskStatus.Pending;
            task.StartedAt = null;
            _logger.LogWarning("Recovered stuck task {TaskId} ({TaskName})", task.Id, task.Name);
        }

        if (stuckTasks.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Recovered {Count} stuck tasks from previous run", stuckTasks.Count);
        }
    }

    private async Task RequeueEligibleTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var now = DateTimeOffset.UtcNow;
        var pendingIds = await context.BackgroundTasks
            .Where(t => t.Status == BackgroundTaskStatus.Pending
                || (t.Status == BackgroundTaskStatus.WaitingForRetry && (t.NextRetryAfter == null || t.NextRetryAfter <= now)))
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.Created)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        foreach (var id in pendingIds)
        {
            _taskQueue.Enqueue(id);
        }

        if (pendingIds.Count > 0)
        {
            _logger.LogInformation("Requeued {Count} pending tasks at startup", pendingIds.Count);
        }
    }

    private async Task RunWorkerAsync(int workerIndex, CancellationToken stoppingToken)
    {
        _logger.LogDebug("Worker {WorkerIndex} started", workerIndex);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _taskQueue.DequeueAsync(stoppingToken);
                await PickAndExecuteNextTaskAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerIndex} encountered an unexpected error", workerIndex);
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogDebug("Worker {WorkerIndex} stopped", workerIndex);
    }

    private async Task PickAndExecuteNextTaskAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var now = DateTimeOffset.UtcNow;
        var task = await context.BackgroundTasks
            .Where(t => t.Status == BackgroundTaskStatus.Pending
                || (t.Status == BackgroundTaskStatus.WaitingForRetry && (t.NextRetryAfter == null || t.NextRetryAfter <= now)))
            .OrderByDescending(t => t.Priority)
            .ThenBy(t => t.Created)
            .FirstOrDefaultAsync(stoppingToken);

        if (task is null)
        {
            return;
        }

        task.Status = BackgroundTaskStatus.InProgress;
        task.StartedAt = DateTimeOffset.UtcNow;
        await context.SaveChangesAsync(stoppingToken);

        var sw = Stopwatch.StartNew();

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(task.TimeoutSeconds));

            var requestType = _typeRegistry.Resolve(task.RequestType);
            if (requestType is null)
            {
                _logger.LogError("Unknown request type {RequestType} for task {TaskId}, marking as failed", task.RequestType, task.Id);
                task.Status = BackgroundTaskStatus.Failed;
                await context.SaveChangesAsync(stoppingToken);
                return;
            }

            var request = JsonSerializer.Deserialize(task.RequestData, requestType);
            if (request is null)
            {
                _logger.LogError("Failed to deserialize task {TaskId} ({TaskName}) with type {RequestType}", task.Id, task.Name, task.RequestType);
                task.Status = BackgroundTaskStatus.Failed;
                await context.SaveChangesAsync(stoppingToken);
                return;
            }

            await sender.Send(request, timeoutCts.Token);

            sw.Stop();
            task.Status = BackgroundTaskStatus.Completed;
            _logger.LogInformation("Task {TaskId} ({TaskName}) completed in {ElapsedMs}ms (attempt {Attempt}/{MaxAttempts})",
                task.Id, task.Name, sw.ElapsedMilliseconds, task.AttemptCount + 1, task.MaxAttempts);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning("Task {TaskId} ({TaskName}) interrupted by host shutdown, will be recovered on next startup", task.Id, task.Name);
            return;
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _logger.LogError("Task {TaskId} ({TaskName}) timed out after {TimeoutSeconds}s", task.Id, task.Name, task.TimeoutSeconds);
            HandleTaskFailure(task);
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Task {TaskId} ({TaskName}) failed after {ElapsedMs}ms (attempt {Attempt}/{MaxAttempts})",
                task.Id, task.Name, sw.ElapsedMilliseconds, task.AttemptCount + 1, task.MaxAttempts);
            HandleTaskFailure(task);
        }

        task.AttemptCount++;
        await context.SaveChangesAsync(stoppingToken);
    }

    private void HandleTaskFailure(BackgroundTask task)
    {
        if (task.AttemptCount + 1 >= task.MaxAttempts)
        {
            task.Status = BackgroundTaskStatus.Failed;
            _logger.LogError("Task {TaskId} ({TaskName}) exhausted all {MaxAttempts} attempts, marked as failed",
                task.Id, task.Name, task.MaxAttempts);
        }
        else
        {
            var delay = TimeSpan.FromSeconds(Math.Min(30 * Math.Pow(2, task.AttemptCount), MaxBackoff.TotalSeconds));
            task.Status = BackgroundTaskStatus.WaitingForRetry;
            task.NextRetryAfter = DateTimeOffset.UtcNow.Add(delay);
            task.StartedAt = null;
            _logger.LogWarning("Task {TaskId} ({TaskName}) will retry after {Delay} (attempt {Attempt}/{MaxAttempts})",
                task.Id, task.Name, delay, task.AttemptCount + 1, task.MaxAttempts);
        }
    }

    private async Task RunOrphanPollerAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(OrphanPollInterval, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                var now = DateTimeOffset.UtcNow;
                var eligibleCount = await context.BackgroundTasks
                    .CountAsync(t => t.Status == BackgroundTaskStatus.Pending
                        || (t.Status == BackgroundTaskStatus.WaitingForRetry && (t.NextRetryAfter == null || t.NextRetryAfter <= now)),
                        stoppingToken);

                if (eligibleCount > 0)
                {
                    _logger.LogDebug("Orphan poller found {Count} eligible tasks, requeueing", eligibleCount);
                    for (var i = 0; i < eligibleCount; i++)
                    {
                        _taskQueue.Enqueue(Guid.Empty);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Orphan poller encountered an error");
            }
        }
    }

    private async Task RunCleanupAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(CleanupInterval, stoppingToken);

                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

                var now = DateTimeOffset.UtcNow;
                var completedCutoff = now - CompletedRetention;
                var failedCutoff = now - FailedRetention;

                var staleCompleted = await context.BackgroundTasks
                    .Where(t => t.Status == BackgroundTaskStatus.Completed && t.LastModified < completedCutoff)
                    .ToListAsync(stoppingToken);

                var staleFailed = await context.BackgroundTasks
                    .Where(t => t.Status == BackgroundTaskStatus.Failed && t.LastModified < failedCutoff)
                    .ToListAsync(stoppingToken);

                var totalRemoved = staleCompleted.Count + staleFailed.Count;
                if (totalRemoved > 0)
                {
                    context.BackgroundTasks.RemoveRange(staleCompleted);
                    context.BackgroundTasks.RemoveRange(staleFailed);
                    await context.SaveChangesAsync(stoppingToken);
                    _logger.LogInformation("Cleaned up {CompletedCount} completed and {FailedCount} failed tasks",
                        staleCompleted.Count, staleFailed.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Cleanup encountered an error");
            }
        }
    }
}
