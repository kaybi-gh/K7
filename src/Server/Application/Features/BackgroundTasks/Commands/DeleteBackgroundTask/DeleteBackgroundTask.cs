using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.BackgroundTasks.Commands.DeleteBackgroundTask;

public record DeleteBackgroundTaskCommand(Guid Id) : IRequest;

public class DeleteBackgroundTaskCommandHandler : IRequestHandler<DeleteBackgroundTaskCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IBackgroundTaskNotifier _notifier;

    public DeleteBackgroundTaskCommandHandler(IApplicationDbContext context, IBackgroundTaskNotifier notifier)
    {
        _context = context;
        _notifier = notifier;
    }

    public async Task Handle(DeleteBackgroundTaskCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.BackgroundTasks
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        _context.BackgroundTasks.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        await _notifier.NotifyBackgroundTaskUpdatedAsync(cancellationToken);
    }
}
