using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.BackgroundTasks.Commands.CancelBackgroundTask;

public record CancelBackgroundTaskCommand(Guid Id) : IRequest;

public class CancelBackgroundTaskCommandHandler : IRequestHandler<CancelBackgroundTaskCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IBackgroundTaskNotifier _notifier;

    public CancelBackgroundTaskCommandHandler(IApplicationDbContext context, IBackgroundTaskNotifier notifier)
    {
        _context = context;
        _notifier = notifier;
    }

    public async Task Handle(CancelBackgroundTaskCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.BackgroundTasks
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        if (entity.Status is not (BackgroundTaskStatus.Pending or BackgroundTaskStatus.InProgress or BackgroundTaskStatus.WaitingForRetry))
        {
            return;
        }

        entity.Status = BackgroundTaskStatus.Failed;
        entity.CompletedAt = DateTimeOffset.UtcNow;
        entity.ErrorDetails = "Cancelled by user";
        await _context.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyBackgroundTaskUpdatedAsync(cancellationToken);
    }
}
