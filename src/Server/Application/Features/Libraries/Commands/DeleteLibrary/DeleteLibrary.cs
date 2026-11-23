using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Helpers;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Features.Libraries.Commands.DeleteLibrary;

[Authorize(Roles = Roles.Administrator)]
public record DeleteLibraryCommand(Guid Id) : IRequest;

public class DeleteLibraryCommandHandler(
    IApplicationDbContext context,
    IHomeLayoutMaintenanceService homeLayoutMaintenanceService,
    IOptions<PathsConfiguration> pathsOptions,
    ILogger<DeleteLibraryCommandHandler> logger) : IRequestHandler<DeleteLibraryCommand>
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
        {
            BackgroundTaskFailure.MarkCancelled(task);
            task.ErrorDetails = "Library deleted";
        }

        context.Libraries.Remove(library);
        await homeLayoutMaintenanceService.RemoveLibraryReferencesAsync(request.Id, cancellationToken);
        library.AddDomainEvent(new LibraryDeletedEvent(library));
        await context.SaveChangesAsync(cancellationToken);

        // The files go with the library through the cascade, without a per-file event, so
        // their pre-extracted subtitles are removed here. Not tied to the current capability
        // value: it may have been enabled in the past.
        foreach (var fileId in fileIds)
            HlsSubtitleVttExtractor.DeletePersistentCache(pathsOptions.Value, fileId, logger);
    }
}
