using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Events;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.Libraries.Commands.DeleteIndexedFile;

public record DeleteIndexedFileCommand(Guid Id) : IRequest;

public class DeleteIndexedFileCommandHandler(
    IApplicationDbContext context,
    ILibraryNotifier notifier,
    ILogger<DeleteIndexedFileCommandHandler> logger) : IRequestHandler<DeleteIndexedFileCommand>
{
    public async Task Handle(DeleteIndexedFileCommand request, CancellationToken cancellationToken)
    {
        foreach (var tracked in context.IndexedFiles.Local.Where(x => x.Id == request.Id).ToList())
        {
            context.Entry(tracked).State = EntityState.Detached;
        }

        var entity = await context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        var formerMediaId = entity.MediaId;
        var libraryId = entity.LibraryId;
        entity.MediaId = null;

        if (entity.FileMetadata is VideoFileMetadata videoMetadata)
        {
            if (videoMetadata.Thumbnails is not null)
            {
                context.MetadataPictures.Remove(videoMetadata.Thumbnails);
                videoMetadata.Thumbnails = null;
            }

            context.FileMetadatas.Remove(entity.FileMetadata);
            entity.FileMetadata = null;
        }
        else if (entity.FileMetadata is not null)
        {
            context.FileMetadatas.Remove(entity.FileMetadata);
            entity.FileMetadata = null;
        }

        context.IndexedFiles.Remove(entity);
        entity.AddDomainEvent(new IndexedFileDeletedEvent(entity, formerMediaId, libraryId));
        await context.SaveChangesAsync(cancellationToken);

        if (formerMediaId is not Guid mediaId)
            return;

        logger.LogDebug(
            "Indexed file removed for media {MediaId}; notifying catalog clients",
            mediaId);

        await notifier.NotifyMediaIndexedFilesUpdatedAsync(mediaId, libraryId, cancellationToken);
    }
}
