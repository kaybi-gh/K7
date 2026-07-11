using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Application.Features.Libraries.Commands.IndexLibraryFiles;

public record IndexLibraryFilesCommand(Guid Id) : IRequest;

public class IndexLibraryFilesCommandHandler : IRequestHandler<IndexLibraryFilesCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileIndexer _fileIndexerService;
    private readonly ILibraryNotifier _libraryNotifier;
    private readonly IMediaQueryCacheInvalidator _cacheInvalidator;

    public IndexLibraryFilesCommandHandler(
        IApplicationDbContext context,
        IFileIndexer fileIndexerService,
        ILibraryNotifier libraryNotifier,
        IMediaQueryCacheInvalidator cacheInvalidator)
    {
        _context = context;
        _fileIndexerService = fileIndexerService;
        _libraryNotifier = libraryNotifier;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task Handle(IndexLibraryFilesCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Libraries
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        var result = await _fileIndexerService.IndexAsync(entity, cancellationToken);

        await _context.ScanIssues
            .Where(s => s.LibraryId == entity.Id)
            .ExecuteDeleteAsync(cancellationToken);

        if (result.InaccessiblePaths.Count > 0)
        {
            var now = DateTimeOffset.UtcNow;
            var newIssues = result.InaccessiblePaths.Select(p => new LibraryScanIssue
            {
                Id = Guid.NewGuid(),
                LibraryId = entity.Id,
                Path = p.Path,
                ErrorMessage = p.Error,
                DetectedAt = now
            });
            _context.ScanIssues.AddRange(newIssues);
        }

        await _context.SaveChangesAsync(cancellationToken);

        _cacheInvalidator.InvalidateAll();
        await _libraryNotifier.NotifyLibraryScanCompletedAsync(entity.Id, result.AddedCount, result.SkippedCount, result.InaccessiblePaths.Count, cancellationToken);
    }
}
