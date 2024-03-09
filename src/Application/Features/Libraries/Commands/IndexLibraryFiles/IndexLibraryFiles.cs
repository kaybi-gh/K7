using MediaServer.Application.Common.Interfaces;
using MediaServer.Domain.Interfaces;

namespace MediaServer.Application.Features.Libraries.Commands.IndexLibraryFiles;

public record IndexLibraryFilesCommand(int Id) : IRequest;

public class IndexLibraryFilesCommandHandler : IRequestHandler<IndexLibraryFilesCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileIndexerService _fileIndexerService;

    public IndexLibraryFilesCommandHandler(IApplicationDbContext context, IFileIndexerService fileIndexerService)
    {
        _context = context;
        _fileIndexerService = fileIndexerService;
    }

    public async Task Handle(IndexLibraryFilesCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Libraries
            .Include(l => l.IndexedFiles)
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        await _fileIndexerService.IndexAsync(entity, cancellationToken);
    }
}
