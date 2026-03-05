using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.IndexedFiles.Commands.RefreshIndexedFile;

public record RefreshIndexedFileCommand(Guid Id) : IRequest;

public class RefreshIndexedFileCommandHandler : IRequestHandler<RefreshIndexedFileCommand>
{
    private readonly IApplicationDbContext _context;

    public RefreshIndexedFileCommandHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
    }

    public async Task Handle(RefreshIndexedFileCommand request, CancellationToken cancellationToken)
    {
        var indexedFile = await _context.IndexedFiles
            .Include(x => x.FileMetadata)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, indexedFile);
        Guard.Against.NullOrEmpty(indexedFile.Path);

        var library = await _context.Libraries.FindAsync([indexedFile.LibraryId], cancellationToken);
        Guard.Against.NotFound(indexedFile.LibraryId, library);

        indexedFile.AddDomainEvent(new IndexedFileCreatedEvent(indexedFile, library.MediaType switch
        {
            LibraryMediaType.Movie => FileType.Video,
            LibraryMediaType.Music => FileType.Audio,
            LibraryMediaType.Serie => FileType.Video,
            _ => throw new InvalidOperationException(),
        }));

        await _context.SaveChangesAsync(cancellationToken);
    }
}
