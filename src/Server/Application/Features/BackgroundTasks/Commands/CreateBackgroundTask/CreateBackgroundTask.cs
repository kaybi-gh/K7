using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
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
    public BackgroundTaskPriority Priority { get; set; } = BackgroundTaskPriority.Lowest;
    public int MaxAttempts { get; set; } = 1;
    public int? TimeoutSeconds { get; set; }
    public string? ConcurrencyGroup { get; set; }
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

        var existingTaskId = await _context.BackgroundTasks
            .Where(t => t.Name == taskName
                && t.TargetEntityId == request.TargetEntityId
                && (t.Status == BackgroundTaskStatus.Pending
                    || t.Status == BackgroundTaskStatus.InProgress
                    || t.Status == BackgroundTaskStatus.WaitingForRetry))
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(cancellationToken);

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
            Priority = request.Priority,
            MaxAttempts = request.MaxAttempts,
            TimeoutSeconds = request.TimeoutSeconds ?? 300,
            ConcurrencyGroup = request.ConcurrencyGroup,
            Status = BackgroundTaskStatus.Pending
        };

        _context.BackgroundTasks.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyBackgroundTaskUpdatedAsync(cancellationToken);

        _taskQueue.Enqueue(entity.Id);

        return entity.Id;
    }
}
