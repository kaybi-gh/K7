using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.IndexedFiles.Commands.RefreshIndexedFile;

public record RefreshIndexedFileCommand(Guid Id) : IRequest;

public class RefreshIndexedFileCommandHandler : IRequestHandler<RefreshIndexedFileCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public RefreshIndexedFileCommandHandler(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
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

        FileType fileType = library.MediaType switch
        {
            LibraryMediaType.Movie => FileType.Video,
            LibraryMediaType.Serie => FileType.Video,
            LibraryMediaType.Music => FileType.Audio,
            _ => throw new NotImplementedException()
        };

        await _sender.Send(new BackgroundTasks.Commands.CreateBackgroundTask.CreateBackgroundTaskCommand()
        {
            Request = new CreateFileMetadatas.CreateFileMetadatasCommand()
            {
                Id = indexedFile.Id,
                FileType = fileType
            },
            Priority = BackgroundTaskPriority.High,
            TargetEntityId = indexedFile.Id,
            TargetEntityTypeName = nameof(Domain.Entities.IndexedFile)
        }, cancellationToken);
    }
}
