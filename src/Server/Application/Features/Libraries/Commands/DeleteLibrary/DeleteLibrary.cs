using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Libraries.Commands.DeleteLibrary;

[Authorize(Roles = Roles.Administrator)]
public record DeleteLibraryCommand(Guid Id) : IRequest;

public class DeleteLibraryCommandHandler(IApplicationDbContext context) : IRequestHandler<DeleteLibraryCommand>
{
    public async Task Handle(DeleteLibraryCommand request, CancellationToken cancellationToken)
    {
        var library = await context.Libraries
            .FindAsync([request.Id], cancellationToken);

        Guard.Against.NotFound(request.Id, library);

        // Cancel pending background tasks that target this library's indexed files
        var fileIds = await context.IndexedFiles
            .Where(f => f.LibraryId == request.Id)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);

        var targetIds = new HashSet<Guid>(fileIds) { request.Id };

        var staleTasks = await context.BackgroundTasks
            .Where(t => (t.Status == BackgroundTaskStatus.Pending
                || t.Status == BackgroundTaskStatus.WaitingForRetry)
                && t.TargetEntityId.HasValue
                && targetIds.Contains(t.TargetEntityId.Value))
            .ToListAsync(cancellationToken);

        foreach (var task in staleTasks)
            task.Status = BackgroundTaskStatus.Failed;

        context.Libraries.Remove(library);
        library.AddDomainEvent(new LibraryDeletedEvent(library));
        await context.SaveChangesAsync(cancellationToken);
    }
}
