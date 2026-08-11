using Ardalis.GuardClauses;
using FluentValidation.Results;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;
using K7.Server.Application.Features.Diagnostics.Services;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Application.Services;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ValidationException = K7.Server.Application.Common.Exceptions.ValidationException;

namespace K7.Server.Application.Features.Libraries.Commands.RematchLibraryMedia;

public record RematchLibraryMediaCommand(Guid LibraryId) : IRequest<int>;

public class RematchLibraryMediaCommandHandler(
    IApplicationDbContext context,
    ISender sender,
    OrphanIndexedFileFixBuilder orphanIndexedFileFixBuilder,
    IMediaLibraryAvailabilityService mediaLibraryAvailabilityService,
    IMediaQueryCacheInvalidator cacheInvalidator,
    ILogger<RematchLibraryMediaCommandHandler> logger)
    : IRequestHandler<RematchLibraryMediaCommand, int>
{
    public async Task<int> Handle(RematchLibraryMediaCommand request, CancellationToken cancellationToken)
    {
        var library = await context.Libraries
            .FirstOrDefaultAsync(l => l.Id == request.LibraryId, cancellationToken);

        Guard.Against.NotFound(request.LibraryId, library);

        if (library.PeerServerId is not null)
        {
            throw new ValidationException(
            [
                new ValidationFailure(
                    nameof(request.LibraryId),
                    "Federated libraries cannot be rematched locally.")
            ]);
        }

        var files = await context.IndexedFiles
            .Where(f => f.LibraryId == library.Id)
            .ToListAsync(cancellationToken);

        if (files.Count == 0)
            return 0;

        // Drop stale CreateMedia from a prior scan before detach: priority alone leaves those tasks
        // to run after rematch and recreate duplicate media for the same files.
        var fileIds = files.Select(f => f.Id).ToList();
        var cancelledCreateMedia = await CancelPendingCreateMediaAsync(fileIds, cancellationToken);

        var attachedCount = 0;
        var formerMediaIdsByIndexedFileId = new Dictionary<Guid, Guid>();
        foreach (var file in files)
        {
            if (!file.MediaId.HasValue)
                continue;

            formerMediaIdsByIndexedFileId[file.Id] = file.MediaId.Value;
            file.MediaId = null;
            attachedCount++;
        }

        await context.SaveChangesAsync(cancellationToken);

        // Detach is committed. Finish enqueue without honouring cancellation so a timeout or
        // operator cancel cannot leave files orphaned with no CreateMedia work queued.
        var enqueueToken = CancellationToken.None;

        await mediaLibraryAvailabilityService.RebuildForLibraryAsync(library.Id, enqueueToken);
        cacheInvalidator.InvalidateAll();

        var tasks = await orphanIndexedFileFixBuilder.BuildCreateMediaTasksAsync(
            fileIds,
            enqueueToken,
            BackgroundTaskTriggeredBy.User,
            formerMediaIdsByIndexedFileId);

        if (tasks.Count > 0)
            await sender.Send(new CreateBackgroundTasksBatchCommand(tasks), enqueueToken);

        logger.LogInformation(
            "Rematch library {LibraryId}: cancelled {CancelledCount} stale CreateMedia, detached {DetachedCount} files, queued {TaskCount} CreateMedia tasks",
            library.Id,
            cancelledCreateMedia,
            attachedCount,
            tasks.Count);

        return tasks.Count;
    }

    private async Task<int> CancelPendingCreateMediaAsync(
        IReadOnlyList<Guid> indexedFileIds,
        CancellationToken cancellationToken)
    {
        if (indexedFileIds.Count == 0)
            return 0;

        var createMediaName = nameof(CreateMediaCommand);
        var staleTasks = await context.BackgroundTasks
            .Where(t => t.Name == createMediaName
                && (t.Status == BackgroundTaskStatus.Pending
                    || t.Status == BackgroundTaskStatus.WaitingForRetry)
                && t.TargetEntityId.HasValue
                && indexedFileIds.Contains(t.TargetEntityId.Value))
            .ToListAsync(cancellationToken);

        foreach (var task in staleTasks)
        {
            BackgroundTaskFailure.MarkCancelled(task);
            task.ErrorDetails = "Superseded by library rematch";
        }

        return staleTasks.Count;
    }
}
