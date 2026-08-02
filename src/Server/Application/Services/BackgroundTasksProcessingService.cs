using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
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
    private readonly IBackgroundTaskCancellationRegistry _cancellationRegistry;
    private readonly MetadataProviderCooldownStore _metadataProviderCooldownStore;
    private readonly ConcurrentDictionary<string, int> _activeCountByLaneKey = new();
    private readonly ConcurrentDictionary<Guid, byte> _executingTaskIds = new();
    private readonly List<WorkerHandle> _workers = [];
    private readonly Lock _workersLock = new();
    private readonly SemaphoreSlim _scaleLock = new(1, 1);
    private int _cachedWorkerCount = 1;
    private Dictionary<BackgroundTaskLane, int> _cachedLaneLimits = new();
    private readonly Lock _settingsCacheLock = new();
    private CancellationToken _stoppingToken;
    private volatile bool _started;

    public BackgroundTasksProcessingService(
        ILogger<BackgroundTasksProcessingService> logger,
        IServiceProvider serviceProvider,
        IBackgroundTaskQueue taskQueue,
        BackgroundTaskTypeRegistry typeRegistry,
        IBackgroundTaskNotifier notifier,
        IBackgroundTaskCancellationRegistry cancellationRegistry,
        MetadataProviderCooldownStore metadataProviderCooldownStore)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _taskQueue = taskQueue;
        _typeRegistry = typeRegistry;
        _notifier = notifier;
        _cancellationRegistry = cancellationRegistry;
        _metadataProviderCooldownStore = metadataProviderCooldownStore;
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

    public IReadOnlyDictionary<string, int> ActiveCountByLaneKey => _activeCountByLaneKey;

    /// <summary>
    /// Reloads persisted worker/lane settings and scales immediately. Called when an admin saves
    /// settings so the change does not wait for the supervisor poll.
    /// </summary>
    public Task ApplySettingsAsync(CancellationToken cancellationToken = default)
    {
        if (!_started || _stoppingToken.IsCancellationRequested)
            return Task.CompletedTask;

        return SyncWorkersToSettingsAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;
        var desiredCount = await ReadWorkerCountAsync(stoppingToken);
        UpdateSettingsCache(desiredCount, await ReadLaneLimitsAsync(stoppingToken));
        _logger.LogInformation("BackgroundTasksProcessingService starting with {WorkerCount} workers", desiredCount);

        await RecoverStuckTasksAsync(stoppingToken);
        await RequeueEligibleTasksAsync(stoppingToken);

        SpawnWorkers(desiredCount, stoppingToken);
        _started = true;

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

    private void UpdateSettingsCache(int workerCount, Dictionary<BackgroundTaskLane, int> laneLimits)
    {
        lock (_settingsCacheLock)
        {
            _cachedWorkerCount = workerCount;
            _cachedLaneLimits = laneLimits;
        }
    }

    private Dictionary<BackgroundTaskLane, int> GetCachedLaneLimits()
    {
        lock (_settingsCacheLock)
            return _cachedLaneLimits;
    }

    private async Task<int> ReadWorkerCountAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IServerSettingsService>();
        var count = await settings.GetAsync(ServerSettingKeys.BackgroundTaskWorkerCount, cancellationToken);
        return BackgroundTaskScheduling.ClampWorkerCount(count);
    }

    private void SignalWorkers(int count)
    {
        for (var i = 0; i < count; i++)
            _taskQueue.Enqueue(Guid.Empty);
    }

    private async Task<Dictionary<BackgroundTaskLane, int>> ReadLaneLimitsAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var settings = scope.ServiceProvider.GetRequiredService<IServerSettingsService>();
        return await settings.GetAsync(ServerSettingKeys.BackgroundTaskLaneLimits, cancellationToken) ?? new();
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
                await SyncWorkersToSettingsAsync(stoppingToken);
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

    private async Task SyncWorkersToSettingsAsync(CancellationToken cancellationToken)
    {
        if (!_started || _stoppingToken.IsCancellationRequested)
            return;

        await _scaleLock.WaitAsync(cancellationToken);
        try
        {
            lock (_workersLock)
            {
                _workers.RemoveAll(w => w.Task.IsCompleted);
            }

            var desired = await ReadWorkerCountAsync(cancellationToken);
            UpdateSettingsCache(desired, await ReadLaneLimitsAsync(cancellationToken));
            int currentActive;

            lock (_workersLock)
            {
                currentActive = _workers.Count(w => !w.ShouldStop);
            }

            if (desired > currentActive)
            {
                var toSpawn = desired - currentActive;
                _logger.LogInformation("Scaling up workers from {Current} to {Desired}", currentActive, desired);
                SpawnWorkers(desired, _stoppingToken);
                // New workers block on an empty channel until signaled; wake them so pending
                // work is picked up without waiting for the orphan poller.
                SignalWorkers(toSpawn);
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

                // Wake marked workers so they observe ShouldStop instead of sitting on Dequeue.
                SignalWorkers(toStop);
            }
        }
        finally
        {
            _scaleLock.Release();
        }
    }

    private async Task RecoverStuckTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var stuckTasks = await context.BackgroundTasks
            .Where(t => t.Status == BackgroundTaskStatus.InProgress)
            .ToListAsync(cancellationToken);

        if (stuckTasks.Count == 0)
            return;

        var now = DateTimeOffset.UtcNow;
        foreach (var task in stuckTasks)
        {
            ApplyOrphanRecovery(task, now, "startup recovery");
        }

        await context.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyBackgroundTaskUpdatedAsync(cancellationToken);
        _logger.LogInformation("Recovered {Count} stuck tasks from previous run", stuckTasks.Count);
    }

    private async Task RequeueEligibleTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var now = DateTimeOffset.UtcNow;
        var pendingIds = await context.BackgroundTasks
            .Where(t => t.Status == BackgroundTaskStatus.Pending
                || (t.Status == BackgroundTaskStatus.WaitingForRetry && (t.NextRetryAfter == null || t.NextRetryAfter <= now)))
            .OrderByDescending(t => t.WorkClass)
            .ThenByDescending(t => t.Priority)
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

            var limits = GetCachedLaneLimits();
            var saturation = BackgroundTaskCandidateSelector.BuildSaturation(
                _activeCountByLaneKey,
                limits,
                _metadataProviderCooldownStore.GetCoolingDownProviders());

            var now = DateTimeOffset.UtcNow;

            // Spillover: exclude saturated lane/provider keys from the candidate window so WorkClass
            // preference cannot starve lower-priority work on free keys (idle workers).
            var candidatesQuery = context.BackgroundTasks
                .Where(t => t.Status == BackgroundTaskStatus.Pending
                    || (t.Status == BackgroundTaskStatus.WaitingForRetry && (t.NextRetryAfter == null || t.NextRetryAfter <= now)));

            candidatesQuery = BackgroundTaskCandidateSelector.ApplySpilloverFilter(candidatesQuery, saturation);

            var candidates = await candidatesQuery
                .OrderByDescending(t => t.WorkClass)
                .ThenByDescending(t => t.Priority)
                .ThenBy(t => t.Created)
                .Select(t => new BackgroundTaskPickCandidate(t.Id, t.Lane, t.FederationPeerId, t.MetadataProviderName))
                .Take(BackgroundTaskScheduling.CandidateFetchCount)
                .ToListAsync(stoppingToken);

            if (candidates.Count == 0)
            {
                return;
            }

            var selected = BackgroundTaskCandidateSelector.TryAcquireNext(
                candidates,
                _activeCountByLaneKey,
                limits,
                saturation,
                out var acquiredKey);

            if (selected is null || acquiredKey is null)
            {
                return;
            }

            var candidate = new { selected.Value.Id };

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
                BackgroundTaskConcurrencyGate.Release(_activeCountByLaneKey, acquiredKey);
                _taskQueue.Enqueue(Guid.Empty);
                return;
            }

            await _notifier.NotifyBackgroundTaskUpdatedAsync(stoppingToken);

            var task = await context.BackgroundTasks.FindAsync([candidate.Id], stoppingToken);
            if (task is null)
            {
                BackgroundTaskConcurrencyGate.Release(_activeCountByLaneKey, acquiredKey);
                _taskQueue.Enqueue(Guid.Empty);
                return;
            }

            _executingTaskIds[task.Id] = 0;
            var sw = Stopwatch.StartNew();
            var outcomePersisted = false;

            // Declared here so the outer handlers can tell an operator cancellation from a timeout.
            using var userCts = new CancellationTokenSource();

            try
            {
                try
                {
                    // Two sources on purpose: a cancellation asked for by an operator must not be
                    // reported as a timeout. Only userCts is registered, and the linked token carries
                    // both it and the timeout to the handler.
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, userCts.Token);
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, task.TimeoutSeconds)));

                    _cancellationRegistry.Register(task.Id, userCts);

                    var requestType = _typeRegistry.Resolve(task.RequestType);
                    if (requestType is null)
                    {
                        _logger.LogError("Unknown request type {RequestType} for task {TaskId}, marking as failed", task.RequestType, task.Id);
                        task.Status = BackgroundTaskStatus.Failed;
                        task.ErrorDetails = $"Unknown request type: {task.RequestType}";
                        task.CompletedAt = DateTimeOffset.UtcNow;
                        task.AttemptCount++;
                        await PersistTaskStateAsync(context, task, stoppingToken);
                        outcomePersisted = true;
                        await _notifier.NotifyBackgroundTaskUpdatedAsync(stoppingToken);
                        return;
                    }

                    var request = JsonSerializer.Deserialize(task.RequestData, requestType);
                    if (request is null)
                    {
                        _logger.LogError("Failed to deserialize task {TaskId} ({TaskName}) with type {RequestType}", task.Id, task.Name, task.RequestType);
                        task.Status = BackgroundTaskStatus.Failed;
                        task.ErrorDetails = $"Failed to deserialize request data for type: {task.RequestType}";
                        task.CompletedAt = DateTimeOffset.UtcNow;
                        task.AttemptCount++;
                        await PersistTaskStateAsync(context, task, stoppingToken);
                        outcomePersisted = true;
                        await _notifier.NotifyBackgroundTaskUpdatedAsync(stoppingToken);
                        return;
                    }

                    var timeout = TimeSpan.FromSeconds(Math.Max(1, task.TimeoutSeconds));
                    var sendTask = sender.Send(request, timeoutCts.Token);
                    try
                    {
                        await sendTask.WaitAsync(timeout, stoppingToken);
                    }
                    catch (TimeoutException) when (!sendTask.IsCompleted)
                    {
                        sw.Stop();
                        // WaitAsync only watches the host token + wall clock. An operator cancel that
                        // the handler ignores therefore surfaces here once the timeout elapses; treat
                        // it as a cancellation, not a retryable timeout.
                        if (userCts.IsCancellationRequested || task.CancellationRequested)
                        {
                            task.ErrorDetails = "Cancelled by user";
                            BackgroundTaskFailure.MarkCancelled(task);
                            await PersistTaskStateAsync(context, task, stoppingToken);
                            outcomePersisted = true;
                            await _notifier.NotifyBackgroundTaskUpdatedAsync(stoppingToken);
                            _logger.LogWarning(
                                "Task {TaskId} ({TaskName}) cancelled by user after {ElapsedMs}ms (handler ignored cancellation token)",
                                task.Id, task.Name, sw.ElapsedMilliseconds);
                        }
                        else
                        {
                            task.ErrorDetails = $"Task timed out after {task.TimeoutSeconds}s";
                            BackgroundTaskFailure.Handle(task, new TimeoutException(task.ErrorDetails), MaxBackoff);
                            LogTaskFailureOutcome(task);
                            task.AttemptCount++;
                            await PersistTaskStateAsync(context, task, stoppingToken);
                            outcomePersisted = true;
                            await _notifier.NotifyBackgroundTaskUpdatedAsync(stoppingToken);
                            _logger.LogError(
                                "Task {TaskId} ({TaskName}) timed out after {TimeoutSeconds}s and was abandoned to free the worker slot",
                                task.Id, task.Name, task.TimeoutSeconds);
                        }

                        scopeOwnedByAbandonedTask = true;
                        _ = ObserveAbandonedTaskAsync(sendTask, scope, task.Id, task.Name);
                        return;
                    }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested && !sendTask.IsCompleted && userCts.IsCancellationRequested)
                    {
                        sw.Stop();
                        task.ErrorDetails = "Cancelled by user";
                        BackgroundTaskFailure.MarkCancelled(task);
                        await PersistTaskStateAsync(context, task, stoppingToken);
                        outcomePersisted = true;
                        await _notifier.NotifyBackgroundTaskUpdatedAsync(stoppingToken);
                        _logger.LogWarning(
                            "Task {TaskId} ({TaskName}) cancelled by user after {ElapsedMs}ms",
                            task.Id, task.Name, sw.ElapsedMilliseconds);

                        scopeOwnedByAbandonedTask = true;
                        _ = ObserveAbandonedTaskAsync(sendTask, scope, task.Id, task.Name);
                        return;
                    }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested && !sendTask.IsCompleted)
                    {
                        sw.Stop();
                        task.ErrorDetails = $"Task timed out after {task.TimeoutSeconds}s";
                        BackgroundTaskFailure.Handle(task, new TimeoutException(task.ErrorDetails), MaxBackoff);
                        LogTaskFailureOutcome(task);
                        task.AttemptCount++;
                        await PersistTaskStateAsync(context, task, stoppingToken);
                        outcomePersisted = true;
                        await _notifier.NotifyBackgroundTaskUpdatedAsync(stoppingToken);
                        _logger.LogError(
                            "Task {TaskId} ({TaskName}) timed out after {TimeoutSeconds}s and was abandoned to free the worker slot",
                            task.Id, task.Name, task.TimeoutSeconds);

                        scopeOwnedByAbandonedTask = true;
                        _ = ObserveAbandonedTaskAsync(sendTask, scope, task.Id, task.Name);
                        return;
                    }

                    sw.Stop();

                    if (executionContext.IsCancelled)
                    {
                        task.ErrorDetails = TruncateErrorDetails(executionContext.CancellationDetails);
                        BackgroundTaskFailure.MarkCancelled(task);
                    }
                    else
                    {
                        task.Status = BackgroundTaskStatus.Completed;
                        task.CompletedAt = DateTimeOffset.UtcNow;
                        task.ErrorDetails = null;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Task {TaskId} ({TaskName}) interrupted by host shutdown, will be recovered on next startup", task.Id, task.Name);
                    return;
                }
                catch (TimeoutException)
                {
                    sw.Stop();
                    task.ErrorDetails = $"Task timed out after {task.TimeoutSeconds}s";
                    BackgroundTaskFailure.Handle(task, new TimeoutException(task.ErrorDetails), MaxBackoff);
                    LogTaskFailureOutcome(task);
                }
                catch (OperationCanceledException) when (userCts.IsCancellationRequested)
                {
                    sw.Stop();
                    task.ErrorDetails = "Cancelled by user";
                    BackgroundTaskFailure.MarkCancelled(task);
                    _logger.LogWarning(
                        "Task {TaskId} ({TaskName}) cancelled by user after {ElapsedMs}ms",
                        task.Id, task.Name, sw.ElapsedMilliseconds);
                }
                catch (OperationCanceledException)
                {
                    sw.Stop();
                    task.ErrorDetails = $"Task timed out after {task.TimeoutSeconds}s";
                    BackgroundTaskFailure.Handle(task, new TimeoutException(task.ErrorDetails), MaxBackoff);
                    LogTaskFailureOutcome(task);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    if (ex is ProviderRateLimitedException rateLimited)
                    {
                        _metadataProviderCooldownStore.Report(rateLimited.ProviderName, rateLimited.RetryAfter);
                    }

                    _logger.LogError(ex, "Task {TaskId} ({TaskName}) failed after {ElapsedMs}ms (attempt {Attempt}/{MaxAttempts})",
                        task.Id, task.Name, sw.ElapsedMilliseconds, task.AttemptCount + 1, task.MaxAttempts);
                    task.ErrorDetails = TruncateErrorDetails(ex.Message);
                    BackgroundTaskFailure.Handle(task, ex, MaxBackoff);
                    LogTaskFailureOutcome(task);
                }
                finally
                {
                    BackgroundTaskConcurrencyGate.Release(_activeCountByLaneKey, acquiredKey);
                    _taskQueue.Enqueue(Guid.Empty);
                }

                if (!outcomePersisted)
                {
                    task.AttemptCount++;
                    await PersistTaskStateAsync(context, task, stoppingToken);

                    if (task.Status == BackgroundTaskStatus.Completed)
                    {
                        _logger.LogInformation(
                            "Task {TaskId} ({TaskName}) completed in {ElapsedMs}ms (attempt {Attempt}/{MaxAttempts}, lane {Lane})",
                            task.Id, task.Name, sw.ElapsedMilliseconds, task.AttemptCount, task.MaxAttempts, task.Lane);
                    }
                    else if (task.Status == BackgroundTaskStatus.Cancelled)
                    {
                        _logger.LogWarning(
                            "Task {TaskId} ({TaskName}) cancelled after {ElapsedMs}ms (attempt {Attempt}/{MaxAttempts}): {ErrorDetails}",
                            task.Id, task.Name, sw.ElapsedMilliseconds, task.AttemptCount, task.MaxAttempts, task.ErrorDetails);
                    }

                    await _notifier.NotifyBackgroundTaskUpdatedAsync(stoppingToken);
                }
            }
            finally
            {
                _executingTaskIds.TryRemove(task.Id, out _);
                _cancellationRegistry.Unregister(task.Id);
            }
        }
        finally
        {
            if (!scopeOwnedByAbandonedTask)
            {
                scope.Dispose();
            }
        }
    }

    private static async Task PersistTaskStateAsync(
        IApplicationDbContext context,
        BackgroundTask task,
        CancellationToken cancellationToken)
    {
        // Persist via ExecuteUpdate so completion cannot be lost if the tracked entity
        // is stale after the atomic claim ExecuteUpdate, or if SaveChanges fails later.
        var now = DateTimeOffset.UtcNow;
        await context.BackgroundTasks
            .Where(t => t.Id == task.Id)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, task.Status)
                .SetProperty(t => t.AttemptCount, task.AttemptCount)
                .SetProperty(t => t.StartedAt, task.StartedAt)
                .SetProperty(t => t.CompletedAt, task.CompletedAt)
                .SetProperty(t => t.NextRetryAfter, task.NextRetryAfter)
                .SetProperty(t => t.ErrorDetails, task.ErrorDetails)
                .SetProperty(t => t.LastModified, now),
                cancellationToken);
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

                await ReclaimOrphanedInProgressTasksAsync(stoppingToken);

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

    private async Task ReclaimOrphanedInProgressTasksAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var now = DateTimeOffset.UtcNow;
        var inProgress = await context.BackgroundTasks
            .Where(t => t.Status == BackgroundTaskStatus.InProgress && t.StartedAt != null)
            .ToListAsync(cancellationToken);

        var reclaimed = 0;
        foreach (var task in inProgress)
        {
            // Still owned by a worker in this process - WaitAsync / abandon handles the timeout.
            if (_executingTaskIds.ContainsKey(task.Id))
            {
                continue;
            }

            var deadline = task.StartedAt!.Value.AddSeconds(Math.Max(1, task.TimeoutSeconds));
            if (now < deadline)
            {
                continue;
            }

            ApplyOrphanRecovery(task, now, "orphan poller");
            reclaimed++;
        }

        if (reclaimed == 0)
        {
            return;
        }

        await context.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyBackgroundTaskUpdatedAsync(cancellationToken);
        SignalWorkers(Math.Min(reclaimed, Math.Max(1, ActiveWorkerCount)));
        _logger.LogInformation("Reclaimed {Count} orphaned InProgress tasks", reclaimed);
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

    /// <summary>
    /// Resolves an <see cref="BackgroundTaskStatus.InProgress"/> row whose worker is gone (process
    /// restart or orphan timeout). Honours operator cancellation and counts reclaim attempts so a
    /// crash-looping task cannot be requeued forever.
    /// </summary>
    private void ApplyOrphanRecovery(BackgroundTask task, DateTimeOffset now, string source)
    {
        if (task.CancellationRequested)
        {
            BackgroundTaskFailure.MarkCancelled(task);
            task.ErrorDetails = "Cancelled by user";
            _logger.LogWarning(
                "Task {TaskId} ({TaskName}) marked cancelled during {Source} (operator had requested cancellation)",
                task.Id, task.Name, source);
            return;
        }

        // Count the reclaim: a task that kills the process (OOM during ffmpeg, for instance) would
        // otherwise be requeued forever without ever incrementing AttemptCount, and so never reach
        // MaxAttempts.
        task.ReclaimCount++;
        task.StartedAt = null;
        task.CompletedAt = null;

        if (task.ReclaimCount > BackgroundTaskScheduling.MaxReclaims)
        {
            task.Status = BackgroundTaskStatus.Failed;
            task.CompletedAt = now;
            task.ErrorDetails = TruncateErrorDetails(
                $"Failed after being reclaimed {task.ReclaimCount} times without completing; the task most likely crashes the process");
            _logger.LogError(
                "Task {TaskId} ({TaskName}) failed after {ReclaimCount} reclaims without completing ({Source})",
                task.Id, task.Name, task.ReclaimCount, source);
            return;
        }

        task.Status = BackgroundTaskStatus.Pending;
        task.ErrorDetails = TruncateErrorDetails(
            $"Reclaimed InProgress task with no active worker ({source}, reclaim {task.ReclaimCount} of {BackgroundTaskScheduling.MaxReclaims})");
        _logger.LogWarning(
            "Reclaimed InProgress task {TaskId} ({TaskName}) via {Source}, reclaim {ReclaimCount} of {MaxReclaims}",
            task.Id, task.Name, source, task.ReclaimCount, BackgroundTaskScheduling.MaxReclaims);
    }

    private sealed class WorkerHandle
    {
        public Task Task { get; set; } = Task.CompletedTask;
        public volatile bool ShouldStop;
    }
}
