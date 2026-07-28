using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.BackgroundTasks.Commands.CancelBackgroundTask;

public record CancelBackgroundTaskCommand(Guid Id) : IRequest;

public class CancelBackgroundTaskCommandHandler : IRequestHandler<CancelBackgroundTaskCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IBackgroundTaskNotifier _notifier;
    private readonly IBackgroundTaskCancellationRegistry _cancellationRegistry;

    public CancelBackgroundTaskCommandHandler(
        IApplicationDbContext context,
        IBackgroundTaskNotifier notifier,
        IBackgroundTaskCancellationRegistry cancellationRegistry)
    {
        _context = context;
        _notifier = notifier;
        _cancellationRegistry = cancellationRegistry;
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

        entity.CancellationRequested = true;

        if (entity.Status == BackgroundTaskStatus.InProgress)
        {
            // Signal the handler and let the worker persist the outcome. Marking it cancelled here
            // would leave the row terminal while the work is still running and holding its lane slot.
            var signalled = _cancellationRegistry.TryCancel(entity.Id);

            if (!signalled)
            {
                // Not running in this process (a leftover row after a crash, for instance): the orphan
                // reclaim would requeue it, so make the cancellation terminal now.
                BackgroundTaskFailure.MarkCancelled(entity);
                entity.ErrorDetails = "Cancelled by user";
            }
        }
        else
        {
            BackgroundTaskFailure.MarkCancelled(entity);
            entity.ErrorDetails = "Cancelled by user";
        }

        await _context.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyBackgroundTaskUpdatedAsync(cancellationToken);
    }
}
