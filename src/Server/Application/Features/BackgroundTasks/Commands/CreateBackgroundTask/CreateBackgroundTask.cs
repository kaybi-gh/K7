using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Application.Common.Interfaces;
using System.Text.Json;

namespace K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;

public record CreateBackgroundTaskCommand : IRequest<Guid>
{
    public required IBaseRequest Request { get; set; }
    public string? TargetEntityTypeName { get; set; }
    public Guid? TargetEntityId { get; set; }
    public BackgroundTaskPriority Priority { get; set; } = BackgroundTaskPriority.Lowest;
    public int MaxRetryCount { get; set; } = 0;
}

public class CreateBackgroundTaskCommandHandler : IRequestHandler<CreateBackgroundTaskCommand, Guid>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public CreateBackgroundTaskCommandHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task<Guid> Handle(CreateBackgroundTaskCommand request, CancellationToken cancellationToken)
    {
        var entity = new BackgroundTask
        {
            Name = request.Request.GetType().Name,
            RequestType = request.Request.GetType().AssemblyQualifiedName!,
            RequestData = JsonSerializer.Serialize(request.Request, request.Request.GetType()),
            TargetEntityType = request.TargetEntityTypeName,
            TargetEntityId = request.TargetEntityId,
            Priority = request.Priority,
            MaxRetryCount = request.MaxRetryCount,
            Status = BackgroundTaskStatus.Pending
        };

        entity.AddDomainEvent(new BackgroundTaskCreatedEvent(entity));
        _context.BackgroundTasks.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
