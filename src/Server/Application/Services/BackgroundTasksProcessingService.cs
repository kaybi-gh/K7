using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;

public class BackgroundTasksProcessingService : BackgroundService
{
    private const int DefaultConcurrencyLimit = 1;
    private static readonly TimeSpan SupervisionInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan OrphanPollInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan CompletedRetention = TimeSpan.FromDays(7);
    private static readonly TimeSpan FailedRetention = TimeSpan.FromDays(30);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromMinutes(15);

    private readonly ILogger<BackgroundTasksProcessingService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly BackgroundTaskTypeRegistry _typeRegistry;
    private readonly IBackgroundTaskNotifier _notifier;
    private readonly ConcurrentDictionary<string, int> _activeCountByGroup = new();
    private readonly List<WorkerHandle> _workers = [];
    private readonly Lock _workersLock = new();
    private int _cachedWorkerCount = 1;
    private Dictionary<string, int> _cachedConcurrencyLimits = new();
    private readonly Lock _settingsCacheLock = new();

    public BackgroundTasksProcessingService(
        ILogger<BackgroundTasksProcessingService> logger,
        IServiceProvider serviceProvider,
        IBackgroundTaskQueue taskQueue,
        BackgroundTaskTypeRegistry typeRegistry,
        IBackgroundTaskNotifier notifier)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _taskQueue = taskQueue;
        _typeRegistry = typeRegistry;
        _notifier = notifier;
    }

    public int ActiveWorkerCount
    {
        get
        {
            lock (_workersLock)
            {
                return _workers.Count(w => !w.ShouldStop);
            }
        }
    }

    public IReadOnlyDictionary<string, int> ActiveCountByGroup => _activeCountByGroup;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var desiredCount = await ReadWorkerCountAsync(stoppingToken);
        UpdateSettingsCache(desiredCount, await ReadConcurrencyLimitsAsync(stoppingToken));
        _logger.LogInformation("BackgroundTasksProcessingService starting with {WorkerCount} workers", desiredCount);

        await RecoverStuckTasksAsync(stoppingToken);
        await RequeueEligibleTasksAsync(stoppingToken);

        SpawnWorkers(desiredCount, stoppingToken);

        var supervisorTask = RunSupervisorAsync(stoppingToken);
        var orphanTask = RunOrphanPollerAsync(stoppingToken);
        var cleanupTask = RunCleanupAsync(stoppingToken);

        await Task.WhenAll(supervisorTask, orphanTask, cleanupTask);

        List<Task> workerTasks;
        lock (_workersLock)
        {
            workerTasks = _workers.Select(w => w.Task).ToList();
        }
        await Task.WhenAll(workerTasks);

        _logger.LogInformation("BackgroundTasksProcessingService stopped");
    }

    private void UpdateSettingsCache(int workerCount, Dictionary<string, int> concurrencyLimits)
    {
        lock (_settingsCacheLock)
        {
            _cachedWorkerCount = workerCount;
            _cachedConcurrencyLimits = concurrencyLimits;
        }
    }

    private Dictionary<string, int> GetCachedConcurrencyLimits()
    {
        lock (_settingsCacheLock)
            return _cachedConcurrencyLimits;
    }

    private async Task<int> ReadWorkerCountAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IServerSettingsService>();
        var count = await settings.GetAsync(ServerSettingKeys.BackgroundTaskWorkerCount, cancellationToken);
        return Math.Max(1, count);
    }

    private void SignalWorkers(int count)
    {
        for (var i = 0; i < count; i++)
            _taskQueue.Enqueue(Guid.Empty);
    }

    private async Task<Dictionary<string, int>> ReadConcurrencyLimitsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IServerSettingsService>();
        return await settings.GetAsync(ServerSettingKeys.BackgroundTaskConcurrencyLimits, cancellationToken) ?? new();
    }

    private void SpawnWorkers(int count, CancellationToken stoppingToken)
    {
        lock (_workersLock)
        {
            var currentActive = _workers.Count(w => !w.ShouldStop);
            var toSpawn = count - currentActive;

            for (var i = 0; i < toSpawn; i++)
            {
                var workerIndex = _workers.Count;
                var handle = new WorkerHandle();
                handle.Task = RunWorkerAsync(workerIndex, handle, stoppingToken);
                _workers.Add(handle);
                _logger.LogDebug("Spawned worker {WorkerIndex}", workerIndex);
            }
        }
    }

    private async Task RunSupervisorAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(SupervisionInterval, stoppingToken);

                lock (_workersLock)
                {
                    _workers.RemoveAll(w => w.Task.IsCompleted);
                }

                var desired = await ReadWorkerCountAsync(stoppingToken);
                UpdateSettingsCache(desired, await ReadConcurrencyLimitsAsync(stoppingToken));
                int currentActive;

                lock (_workersLock)
                {
                    currentActive = _workers.Count(w => !w.ShouldStop);
                }

                if (desired > currentActive)
                {
                    _logger.LogInformation("Scaling up workers from {Current} to {Desired}", currentActive, desired);
                    SpawnWorkers(desired, stoppingToken);
                }
                else if (desired < currentActive)
                {
                    _logger.LogInformation("Scaling down workers from {Current} to {Desired}", currentActive, desired);
                    var toStop = currentActive - desired;

                    lock (_workersLock)
                    {
                        foreach (var worker in _workers.Where(w => !w.ShouldStop).TakeLast(toStop))
                        {
                            worker.ShouldStop = true;
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Supervisor encountered an error");
            }
        }
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
            task.CompletedAt = null;
            _logger.LogWarning("Recovered stuck task {TaskId} ({TaskName})", task.Id, task.Name);
        }

        if (stuckTasks.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
            await _notifier.NotifyBackgroundTaskUpdatedAsync(cancellationToken);
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

        if (pendingIds.Count > 0)
        {
            var signals = Math.Min(pendingIds.Count, Math.Max(1, _cachedWorkerCount));
            SignalWorkers(signals);
            _logger.LogInformation("Signaled {SignalCount} workers for {PendingCount} pending tasks at startup", signals, pendingIds.Count);
        }
    }

    private async Task RunWorkerAsync(int workerIndex, WorkerHandle handle, CancellationToken stoppingToken)
    {
        _logger.LogDebug("Worker {WorkerIndex} started", workerIndex);

        while (!stoppingToken.IsCancellationRequested && !handle.ShouldStop)
        {
            try
            {
                await _taskQueue.DequeueAsync(stoppingToken);

                if (handle.ShouldStop)
                {
                    break;
                }

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
        var scope = _serviceProvider.CreateScope();
        var scopeOwnedByAbandonedTask = false;

        try
        {
            var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();
            var executionContext = scope.ServiceProvider.GetRequiredService<IBackgroundTaskExecutionContext>();
            executionContext.Reset();

            var limits = GetCachedConcurrencyLimits();
            var saturatedGroups = _activeCountByGroup
                .Where(kvp => kvp.Value >= limits.GetValueOrDefault(kvp.Key, DefaultConcurrencyLimit))
                .Select(kvp => kvp.Key)
                .ToHashSet();

            var now = DateTimeOffset.UtcNow;
            var query = context.BackgroundTasks
                .Where(t => t.Status == BackgroundTaskStatus.Pending
                    || (t.Status == BackgroundTaskStatus.WaitingForRetry && (t.NextRetryAfter == null || t.NextRetryAfter <= now)));

            if (saturatedGroups.Count > 0)
            {
                query = query.Where(t => t.ConcurrencyGroup == null || !saturatedGroups.Contains(t.ConcurrencyGroup));
            }

            var candidate = await query
                .OrderByDescending(t => t.Priority)
                .ThenBy(t => t.Created)
                .Select(t => new { t.Id, t.ConcurrencyGroup })
                .FirstOrDefaultAsync(stoppingToken);

            if (candidate is null)
            {
                return;
            }

            var groupLimit = candidate.ConcurrencyGroup is null
                ? DefaultConcurrencyLimit
                : limits.GetValueOrDefault(candidate.ConcurrencyGroup, DefaultConcurrencyLimit);
            if (!BackgroundTaskConcurrencyGate.TryAcquire(_activeCountByGroup, candidate.ConcurrencyGroup, groupLimit))
            {
                return;
            }

            var acquiredGroup = candidate.ConcurrencyGroup;

            // Atomically claim the task: only succeeds if it is still Pending/WaitingForRetry.
            // This prevents duplicate execution when multiple workers race on the same task.
            var claimTime = DateTimeOffset.UtcNow;
            var claimed = await context.BackgroundTasks
                .Where(t => t.Id == candidate.Id
                    && (t.Status == BackgroundTaskStatus.Pending
                        || (t.Status == BackgroundTaskStatus.WaitingForRetry && (t.NextRetryAfter == null || t.NextRetryAfter <= claimTime))))
                .ExecuteUpdateAsync(s => s
                    .SetProperty(t => t.Status, BackgroundTaskStatus.InProgress)
                    .SetProperty(t => t.StartedAt, claimTime)
                    .SetProperty(t => t.CompletedAt, (DateTimeOffset?)null)
                    .SetProperty(t => t.LastModified, claimTime),
                    stoppingToken);

            if (claimed == 0)
            {
                BackgroundTaskConcurrencyGate.Release(_activeCountByGroup, acquiredGroup);
                _taskQueue.Enqueue(Guid.Empty);
                return;
            }

            await _notifier.NotifyBackgroundTaskUpdatedAsync(stoppingToken);

            var task = await context.BackgroundTasks.FindAsync([candidate.Id], stoppingToken);
            if (task is null)
            {
                BackgroundTaskConcurrencyGate.Release(_activeCountByGroup, acquiredGroup);
                _taskQueue.Enqueue(Guid.Empty);
                return;
            }

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
                    task.ErrorDetails = $"Unknown request type: {task.RequestType}";
                    await context.SaveChangesAsync(stoppingToken);
                    await _notifier.NotifyBackgroundTaskUpdatedAsync(stoppingToken);
                    return;
                }

                var request = JsonSerializer.Deserialize(task.RequestData, requestType);
                if (request is null)
                {
                    _logger.LogError("Failed to deserialize task {TaskId} ({TaskName}) with type {RequestType}", task.Id, task.Name, task.RequestType);
                    task.Status = BackgroundTaskStatus.Failed;
                    task.ErrorDetails = $"Failed to deserialize request data for type: {task.RequestType}";
                    await context.SaveChangesAsync(stoppingToken);
                    await _notifier.NotifyBackgroundTaskUpdatedAsync(stoppingToken);
                    return;
                }

                // WaitAsync lets the worker abandon hung sync I/O when the timeout fires,
                // so concurrency slots are released instead of staying zombie InProgress.
                var sendTask = sender.Send(request, timeoutCts.Token);
                try
                {
                    await sendTask.WaitAsync(timeoutCts.Token);
                }
                catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested && !sendTask.IsCompleted)
                {
                    sw.Stop();
                    _logger.LogError(
                        "Task {TaskId} ({TaskName}) timed out after {TimeoutSeconds}s and was abandoned to free the worker slot",
                        task.Id, task.Name, task.TimeoutSeconds);
                    task.ErrorDetails = $"Task timed out after {task.TimeoutSeconds}s";
                    BackgroundTaskFailure.Handle(task, new TimeoutException(task.ErrorDetails), MaxBackoff);
                    LogTaskFailureOutcome(task);

                    task.AttemptCount++;
                    await context.SaveChangesAsync(stoppingToken);
                    await _notifier.NotifyBackgroundTaskUpdatedAsync(stoppingToken);

                    scopeOwnedByAbandonedTask = true;
                    _ = ObserveAbandonedTaskAsync(sendTask, scope, task.Id, task.Name);
                    return;
                }

                sw.Stop();

                if (executionContext.IsCancelled)
                {
                    task.ErrorDetails = TruncateErrorDetails(executionContext.CancellationDetails);
                    BackgroundTaskFailure.MarkCancelled(task);
                    _logger.LogWarning(
                        "Task {TaskId} ({TaskName}) cancelled after {ElapsedMs}ms (attempt {Attempt}/{MaxAttempts}): {ErrorDetails}",
                        task.Id, task.Name, sw.ElapsedMilliseconds, task.AttemptCount + 1, task.MaxAttempts, task.ErrorDetails);
                }
                else
                {
                    task.Status = BackgroundTaskStatus.Completed;
                    task.CompletedAt = DateTimeOffset.UtcNow;
                    task.ErrorDetails = null;
                    _logger.LogInformation("Task {TaskId} ({TaskName}) completed in {ElapsedMs}ms (attempt {Attempt}/{MaxAttempts}, group {ConcurrencyGroup})",
                        task.Id, task.Name, sw.ElapsedMilliseconds, task.AttemptCount + 1, task.MaxAttempts, task.ConcurrencyGroup ?? "none");
                }
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
                task.ErrorDetails = $"Task timed out after {task.TimeoutSeconds}s";
                BackgroundTaskFailure.Handle(task, new TimeoutException(task.ErrorDetails), MaxBackoff);
                LogTaskFailureOutcome(task);
            }
            catch (Exception ex)
            {
                sw.Stop();
                _logger.LogError(ex, "Task {TaskId} ({TaskName}) failed after {ElapsedMs}ms (attempt {Attempt}/{MaxAttempts})",
                    task.Id, task.Name, sw.ElapsedMilliseconds, task.AttemptCount + 1, task.MaxAttempts);
                task.ErrorDetails = TruncateErrorDetails(ex.Message);
                BackgroundTaskFailure.Handle(task, ex, MaxBackoff);
                LogTaskFailureOutcome(task);
            }
            finally
            {
                if (acquiredGroup is not null)
                {
                    BackgroundTaskConcurrencyGate.Release(_activeCountByGroup, acquiredGroup);
                    _taskQueue.Enqueue(Guid.Empty);
                }
            }

            task.AttemptCount++;
            await context.SaveChangesAsync(stoppingToken);
            await _notifier.NotifyBackgroundTaskUpdatedAsync(stoppingToken);
        }
        finally
        {
            if (!scopeOwnedByAbandonedTask)
            {
                scope.Dispose();
            }
        }
    }

    private async Task ObserveAbandonedTaskAsync(Task sendTask, IServiceScope scope, Guid taskId, string taskName)
    {
        try
        {
            await sendTask;
            _logger.LogWarning(
                "Abandoned timed-out task {TaskId} ({TaskName}) completed after the worker slot was released",
                taskId, taskName);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Abandoned timed-out task {TaskId} ({TaskName}) ended after the worker slot was released",
                taskId, taskName);
        }
        finally
        {
            scope.Dispose();
        }
    }

    private void LogTaskFailureOutcome(BackgroundTask task)
    {
        if (task.Status == BackgroundTaskStatus.Failed)
        {
            _logger.LogError("Task {TaskId} ({TaskName}) exhausted all {MaxAttempts} attempts, marked as failed",
                task.Id, task.Name, task.MaxAttempts);
        }
        else if (task.Status == BackgroundTaskStatus.WaitingForRetry)
        {
            _logger.LogWarning("Task {TaskId} ({TaskName}) will retry after {Delay} (attempt {Attempt}/{MaxAttempts})",
                task.Id, task.Name, task.NextRetryAfter - DateTimeOffset.UtcNow, task.AttemptCount + 1, task.MaxAttempts);
        }
    }

    private static string? TruncateErrorDetails(string? errorDetails) =>
        errorDetails is { Length: > 2000 } ? errorDetails[..2000] : errorDetails;

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
                    var workers = ActiveWorkerCount;
                    var signals = Math.Min(eligibleCount, Math.Max(1, workers));
                    _logger.LogDebug("Orphan poller found {Count} eligible tasks, signaling {SignalCount} workers", eligibleCount, signals);
                    SignalWorkers(signals);
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

                var completedRemoved = await context.BackgroundTasks
                    .Where(t => t.Status == BackgroundTaskStatus.Completed && t.LastModified < completedCutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                var terminalRemoved = await context.BackgroundTasks
                    .Where(t => (t.Status == BackgroundTaskStatus.Failed || t.Status == BackgroundTaskStatus.Cancelled)
                        && t.LastModified < failedCutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                var totalRemoved = completedRemoved + terminalRemoved;
                if (totalRemoved > 0)
                {
                    await _notifier.NotifyBackgroundTaskUpdatedAsync(stoppingToken);
                    _logger.LogInformation(
                        "Cleaned up {CompletedCount} completed and {TerminalCount} failed/cancelled tasks",
                        completedRemoved,
                        terminalRemoved);
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

    private sealed class WorkerHandle
    {
        public Task Task { get; set; } = Task.CompletedTask;
        public volatile bool ShouldStop;
    }
}
