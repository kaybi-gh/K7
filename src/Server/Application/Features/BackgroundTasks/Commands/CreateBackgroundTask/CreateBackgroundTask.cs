using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;

public record CreateBackgroundTaskCommand : IRequest<Guid>
{
    public required IBaseRequest Request { get; set; }
    public string? TargetEntityTypeName { get; set; }
    public Guid? TargetEntityId { get; set; }
    public BackgroundTaskPriority Priority { get; set; } = BackgroundTaskPriority.Lowest;
    public int MaxAttempts { get; set; } = 1;
    public string? ConcurrencyGroup { get; set; }
}

public class CreateBackgroundTaskCommandHandler : IRequestHandler<CreateBackgroundTaskCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IBackgroundTaskQueue _taskQueue;
    private readonly IBackgroundTaskNotifier _notifier;

    public CreateBackgroundTaskCommandHandler(IApplicationDbContext context, IBackgroundTaskQueue taskQueue, IBackgroundTaskNotifier notifier)
    {
        _context = context;
        _taskQueue = taskQueue;
        _notifier = notifier;
    }

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
