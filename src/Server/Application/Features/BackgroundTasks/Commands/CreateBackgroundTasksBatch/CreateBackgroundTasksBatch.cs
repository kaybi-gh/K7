using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;

public record CreateBackgroundTasksBatchItem
{
    public required IBaseRequest Request { get; init; }
    public string? TargetEntityTypeName { get; init; }
    public Guid? TargetEntityId { get; init; }
    public BackgroundTaskPriority Priority { get; init; } = BackgroundTaskPriority.Lowest;
    public int MaxAttempts { get; init; } = 1;
    public int? TimeoutSeconds { get; init; }
    public string? ConcurrencyGroup { get; init; }
}

public record CreateBackgroundTasksBatchCommand(List<CreateBackgroundTasksBatchItem> Items) : IRequest;

public class CreateBackgroundTasksBatchCommandHandler : IRequestHandler<CreateBackgroundTasksBatchCommand>
{
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

        var existingTasks = await _context.BackgroundTasks
            .Where(t => taskNames.Contains(t.Name)
                && t.TargetEntityId.HasValue
                && targetEntityIds.Contains(t.TargetEntityId.Value)
                && (t.Status == BackgroundTaskStatus.Pending
                    || t.Status == BackgroundTaskStatus.InProgress
                    || t.Status == BackgroundTaskStatus.WaitingForRetry))
            .Select(t => new { t.Name, t.TargetEntityId })
            .ToListAsync(cancellationToken);

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
                Priority = item.Priority,
                MaxAttempts = item.MaxAttempts,
                TimeoutSeconds = item.TimeoutSeconds ?? 300,
                ConcurrencyGroup = item.ConcurrencyGroup,
                Status = BackgroundTaskStatus.Pending
            };

            newTasks.Add(entity);
        }

        if (deduplicatedCount > 0)
        {
            _logger.LogWarning("Background tasks batch: {DeduplicatedCount} tasks deduplicated out of {TotalCount}", deduplicatedCount, request.Items.Count);
        }

        if (newTasks.Count == 0) return;

        _context.BackgroundTasks.AddRange(newTasks);
        await _context.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyBackgroundTaskUpdatedAsync(cancellationToken);

        foreach (var task in newTasks)
        {
            _taskQueue.Enqueue(task.Id);
        }

        _logger.LogInformation("Background tasks batch: created {Count} tasks", newTasks.Count);
    }
}
