using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;

public record CreateBackgroundTaskCommand : IRequest<Guid>
{
    public required IBaseRequest Request { get; set; }
    public string? TargetEntityTypeName { get; set; }
    public Guid? TargetEntityId { get; set; }

    /// <summary>Local resource the task competes for.</summary>
    public required BackgroundTaskLane Lane { get; set; }

    /// <summary>Scheduling band: what the task contributes to on the critical path.</summary>
    public required BackgroundTaskWorkClass WorkClass { get; set; }

    /// <summary>Provenance. A <see cref="BackgroundTaskTriggeredBy.User"/> task gets an interactive boost.</summary>
    public required BackgroundTaskTriggeredBy TriggeredBy { get; set; }

    /// <summary>Peer owning the task, for <see cref="BackgroundTaskLane.Federation"/>.</summary>
    public Guid? FederationPeerId { get; set; }

    /// <summary>
    /// Logical external provider for Metadata admission (tmdb, tvdb, local, ...).
    /// Required when <see cref="Lane"/> is <see cref="BackgroundTaskLane.Metadata"/>.
    /// </summary>
    public string? MetadataProviderName { get; set; }

    public int MaxAttempts { get; set; } = 1;
    public int? TimeoutSeconds { get; set; }
}

public class CreateBackgroundTaskCommandHandler(IApplicationDbContext context, IBackgroundTaskQueue taskQueue, IBackgroundTaskNotifier notifier, ILogger<CreateBackgroundTaskCommandHandler> logger)
    : IRequestHandler<CreateBackgroundTaskCommand, Guid>
{
    private readonly IApplicationDbContext _context = context;
    private readonly IBackgroundTaskQueue _taskQueue = taskQueue;
    private readonly IBackgroundTaskNotifier _notifier = notifier;
    private readonly ILogger _logger = logger;

    public async Task<Guid> Handle(CreateBackgroundTaskCommand request, CancellationToken cancellationToken)
    {
        var requestType = request.Request.GetType();
        var taskName = requestType.Name;

        var existingTaskId = await FindActiveTaskIdAsync(taskName, request.TargetEntityId, cancellationToken);
        if (existingTaskId is not null)
        {
            _logger.LogWarning("Background task deduplicated: {TaskName} with TargetEntityId={TargetEntityId} already exists as {ExistingTaskId}",
                taskName, request.TargetEntityId, existingTaskId.Value);
            return existingTaskId.Value;
        }

        var entity = new BackgroundTask
        {
            Name = taskName,
            RequestType = requestType.FullName!,
            RequestData = JsonSerializer.Serialize(request.Request, requestType),
            TargetEntityType = request.TargetEntityTypeName,
            TargetEntityId = request.TargetEntityId,
            Lane = request.Lane,
            WorkClass = request.WorkClass,
            TriggeredBy = request.TriggeredBy,
            Priority = GetInitialPriority(request.TriggeredBy),
            FederationPeerId = request.FederationPeerId,
            MetadataProviderName = MetadataProviderHostMapper.NormalizeForBackgroundTask(request.Lane, request.MetadataProviderName),
            MaxAttempts = request.MaxAttempts,
            TimeoutSeconds = request.TimeoutSeconds ?? 300,
            Status = BackgroundTaskStatus.Pending
        };

        _context.BackgroundTasks.Add(entity);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The unique filtered index on (Name, TargetEntityId) over active statuses turns a
            // concurrent enqueue into a conflict instead of a duplicate. Losing the race is normal
            // (a watcher event and a scheduled scan can fire together), so resolve to the winner.
            _context.BackgroundTasks.Remove(entity);

            var winnerId = await FindActiveTaskIdAsync(taskName, request.TargetEntityId, cancellationToken);
            if (winnerId is null)
                throw;

            _logger.LogInformation("Background task enqueue lost the race for {TaskName} on {TargetEntityId}, reusing {WinnerId}",
                taskName, request.TargetEntityId, winnerId.Value);
            return winnerId.Value;
        }

        await _notifier.NotifyBackgroundTaskUpdatedAsync(cancellationToken);

        _taskQueue.Enqueue(entity.Id);

        return entity.Id;
    }

    private async Task<Guid?> FindActiveTaskIdAsync(string taskName, Guid? targetEntityId, CancellationToken cancellationToken)
        => await _context.BackgroundTasks
            .Where(t => t.Name == taskName
                && t.TargetEntityId == targetEntityId
                && (t.Status == BackgroundTaskStatus.Pending
                    || t.Status == BackgroundTaskStatus.InProgress
                    || t.Status == BackgroundTaskStatus.WaitingForRetry))
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static int GetInitialPriority(BackgroundTaskTriggeredBy triggeredBy)
        => triggeredBy == BackgroundTaskTriggeredBy.User ? BackgroundTaskScheduling.InteractiveBoost : 0;
}
