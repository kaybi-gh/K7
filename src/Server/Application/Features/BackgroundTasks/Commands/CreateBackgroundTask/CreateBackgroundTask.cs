using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using System.Text.Json;

namespace K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;

public record CreateBackgroundTaskCommand : IRequest<Guid>
{
    public required IBaseRequest Request { get; set; }
    public string? TargetEntityTypeName { get; set; }
    public Guid? TargetEntityId { get; set; }
    public BackgroundTaskPriority Priority { get; set; } = BackgroundTaskPriority.Lowest;
    public int MaxAttempts { get; set; } = 1;
}

public class CreateBackgroundTaskCommandHandler : IRequestHandler<CreateBackgroundTaskCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly IBackgroundTaskQueue _taskQueue;

    public CreateBackgroundTaskCommandHandler(IApplicationDbContext context, IBackgroundTaskQueue taskQueue)
    {
        _context = context;
        _taskQueue = taskQueue;
    }

    public async Task<Guid> Handle(CreateBackgroundTaskCommand request, CancellationToken cancellationToken)
    {
        var requestType = request.Request.GetType();

        var entity = new BackgroundTask
        {
            Name = requestType.Name,
            RequestType = requestType.FullName!,
            RequestData = JsonSerializer.Serialize(request.Request, requestType),
            TargetEntityType = request.TargetEntityTypeName,
            TargetEntityId = request.TargetEntityId,
            Priority = request.Priority,
            MaxAttempts = request.MaxAttempts,
            Status = BackgroundTaskStatus.Pending
        };

        _context.BackgroundTasks.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        _taskQueue.Enqueue(entity.Id);

        return entity.Id;
    }
}
