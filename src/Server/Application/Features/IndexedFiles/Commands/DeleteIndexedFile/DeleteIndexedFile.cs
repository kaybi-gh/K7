using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Libraries.Commands.DeleteIndexedFile;

public record DeleteIndexedFileCommand(Guid Id) : IRequest;

public class DeleteIndexedFileCommandHandler : IRequestHandler<DeleteIndexedFileCommand>
{
    private readonly IApplicationDbContext _context;

    public DeleteIndexedFileCommandHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task Handle(DeleteIndexedFileCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        entity.MediaId = null;

        if (entity.FileMetadata is VideoFileMetadata videoMetadata)
        {
            if (videoMetadata.Thumbnails is not null)
            {
                _context.MetadataPictures.Remove(videoMetadata.Thumbnails);
                videoMetadata.Thumbnails = null;
            }

            _context.FileMetadatas.Remove(entity.FileMetadata);
            entity.FileMetadata = null;
        }
        else if (entity.FileMetadata is not null)
        {
            _context.FileMetadatas.Remove(entity.FileMetadata);
            entity.FileMetadata = null;
        }

        _context.IndexedFiles.Remove(entity);
        entity.AddDomainEvent(new IndexedFileDeletedEvent(entity));
        await _context.SaveChangesAsync(cancellationToken);
    }
}
