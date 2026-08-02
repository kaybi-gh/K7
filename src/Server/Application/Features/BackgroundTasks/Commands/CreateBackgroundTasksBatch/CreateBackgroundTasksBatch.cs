using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;

public record CreateBackgroundTasksBatchItem
{
    public required IBaseRequest Request { get; init; }
    public string? TargetEntityTypeName { get; init; }
    public Guid? TargetEntityId { get; init; }

    /// <summary>Local resource the task competes for.</summary>
    public required BackgroundTaskLane Lane { get; init; }

    /// <summary>Scheduling band: what the task contributes to on the critical path.</summary>
    public required BackgroundTaskWorkClass WorkClass { get; init; }

    /// <summary>Provenance. A <see cref="BackgroundTaskTriggeredBy.User"/> task gets an interactive boost.</summary>
    public required BackgroundTaskTriggeredBy TriggeredBy { get; init; }

    /// <summary>Peer owning the task, for <see cref="BackgroundTaskLane.Federation"/>.</summary>
    public Guid? FederationPeerId { get; init; }

    /// <summary>
    /// Logical external provider for Metadata admission.
    /// Required when <see cref="Lane"/> is <see cref="BackgroundTaskLane.Metadata"/>.
    /// </summary>
    public string? MetadataProviderName { get; init; }

    public int MaxAttempts { get; init; } = 1;
    public int? TimeoutSeconds { get; init; }
}

public record CreateBackgroundTasksBatchCommand(List<CreateBackgroundTasksBatchItem> Items) : IRequest;

public class CreateBackgroundTasksBatchCommandHandler : IRequestHandler<CreateBackgroundTasksBatchCommand>
{
    private const int ExistingTaskQueryBatchSize = 400;

    private readonly IApplicationDbContext _context;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IBackgroundTaskNotifier _notifier;
    private readonly ILogger<CreateBackgroundTasksBatchCommandHandler> _logger;

    public CreateBackgroundTasksBatchCommandHandler(IApplicationDbContext context, IBackgroundTaskQueue taskQueue, IBackgroundTaskNotifier notifier, ILogger<CreateBackgroundTasksBatchCommandHandler> logger)
    {
        _context = context;
        _taskQueue = taskQueue;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task Handle(CreateBackgroundTasksBatchCommand request, CancellationToken cancellationToken)
    {
        if (request.Items.Count == 0) return;

        var itemsWithMeta = request.Items
            .Select(item => (Item: item, TaskName: item.Request.GetType().Name, RequestType: item.Request.GetType()))
            .ToList();

        var targetEntityIds = itemsWithMeta
            .Where(x => x.Item.TargetEntityId.HasValue)
            .Select(x => x.Item.TargetEntityId!.Value)
            .ToHashSet();

        var taskNames = itemsWithMeta
            .Select(x => x.TaskName)
            .ToHashSet();

        List<(string Name, Guid? TargetEntityId)> existingTasks = [];
        foreach (var targetEntityIdBatch in targetEntityIds.Chunk(ExistingTaskQueryBatchSize))
        {
            foreach (var taskNameBatch in taskNames.Chunk(ExistingTaskQueryBatchSize))
            {
                var batchTasks = await _context.BackgroundTasks
                    .Where(t => taskNameBatch.Contains(t.Name)
                        && t.TargetEntityId.HasValue
                        && targetEntityIdBatch.Contains(t.TargetEntityId.Value)
                        && (t.Status == BackgroundTaskStatus.Pending
                            || t.Status == BackgroundTaskStatus.InProgress
                            || t.Status == BackgroundTaskStatus.WaitingForRetry))
                    .Select(t => new { t.Name, t.TargetEntityId })
                    .ToListAsync(cancellationToken);

                existingTasks.AddRange(batchTasks.Select(t => (t.Name, t.TargetEntityId)));
            }
        }

        var existingSet = existingTasks
            .Select(t => (t.Name, t.TargetEntityId))
            .ToHashSet();

        List<BackgroundTask> newTasks = [];
        var deduplicatedCount = 0;

        foreach (var (item, taskName, requestType) in itemsWithMeta)
        {
            if (item.TargetEntityId.HasValue && existingSet.Contains((taskName, item.TargetEntityId)))
            {
                deduplicatedCount++;
                continue;
            }

            var entity = new BackgroundTask
            {
                Name = taskName,
                RequestType = requestType.FullName!,
                RequestData = JsonSerializer.Serialize(item.Request, requestType),
                TargetEntityType = item.TargetEntityTypeName,
                TargetEntityId = item.TargetEntityId,
                Lane = item.Lane,
                WorkClass = item.WorkClass,
                TriggeredBy = item.TriggeredBy,
                Priority = item.TriggeredBy == BackgroundTaskTriggeredBy.User
                    ? BackgroundTaskScheduling.InteractiveBoost
                    : 0,
                FederationPeerId = item.FederationPeerId,
                MetadataProviderName = MetadataProviderHostMapper.NormalizeForBackgroundTask(
                    item.Lane,
                    item.MetadataProviderName),
                MaxAttempts = item.MaxAttempts,
                TimeoutSeconds = item.TimeoutSeconds ?? 300,
                Status = BackgroundTaskStatus.Pending
            };

            newTasks.Add(entity);
        }

        if (newTasks.Count == 0)
        {
            if (deduplicatedCount > 0)
            {
                _logger.LogWarning("Background tasks batch: {DeduplicatedCount} tasks deduplicated out of {TotalCount}", deduplicatedCount, request.Items.Count);
            }

            return;
        }

        _context.BackgroundTasks.AddRange(newTasks);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Same race as CreateBackgroundTask: progressive scan + watcher can collide on the unique
            // active-task index. Detach the batch and fall back to one-by-one so a single conflict
            // does not discard the rest of the enqueue.
            foreach (var task in newTasks)
            {
                var entry = _context.Entry(task);
                if (entry.State != EntityState.Detached)
                    entry.State = EntityState.Detached;
            }

            var created = new List<BackgroundTask>();
            foreach (var task in newTasks)
            {
                if (task.TargetEntityId is Guid targetId
                    && await FindActiveTaskIdAsync(task.Name, targetId, cancellationToken) is not null)
                {
                    deduplicatedCount++;
                    continue;
                }

                _context.BackgroundTasks.Add(task);
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    created.Add(task);
                }
                catch (DbUpdateException)
                {
                    var entry = _context.Entry(task);
                    if (entry.State != EntityState.Detached)
                        entry.State = EntityState.Detached;

                    if (task.TargetEntityId is Guid racedTarget
                        && await FindActiveTaskIdAsync(task.Name, racedTarget, cancellationToken) is not null)
                    {
                        deduplicatedCount++;
                        continue;
                    }

                    throw;
                }
            }

            newTasks = created;
        }

        if (deduplicatedCount > 0)
        {
            _logger.LogWarning("Background tasks batch: {DeduplicatedCount} tasks deduplicated out of {TotalCount}", deduplicatedCount, request.Items.Count);
        }

        if (newTasks.Count == 0) return;

        await _notifier.NotifyBackgroundTaskUpdatedAsync(cancellationToken);

        foreach (var task in newTasks)
        {
            _taskQueue.Enqueue(task.Id);
        }

        _logger.LogInformation("Background tasks batch: created {Count} tasks", newTasks.Count);
    }

    private async Task<Guid?> FindActiveTaskIdAsync(string taskName, Guid targetEntityId, CancellationToken cancellationToken)
        => await _context.BackgroundTasks
            .Where(t => t.Name == taskName
                && t.TargetEntityId == targetEntityId
                && (t.Status == BackgroundTaskStatus.Pending
                    || t.Status == BackgroundTaskStatus.InProgress
                    || t.Status == BackgroundTaskStatus.WaitingForRetry))
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
