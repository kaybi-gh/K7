using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.BackgroundTasks.Commands.DeleteBackgroundTask;

public record DeleteBackgroundTaskCommand(Guid Id) : IRequest;

public class DeleteBackgroundTaskCommandHandler : IRequestHandler<DeleteBackgroundTaskCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteBackgroundTaskCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteBackgroundTaskCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.BackgroundTasks
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        _context.BackgroundTasks.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
