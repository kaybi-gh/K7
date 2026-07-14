using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Libraries.Commands.IndexLibraryPaths;

public record IndexLibraryPathsCommand(Guid LibraryId, IReadOnlyList<string> Paths) : IRequest;

public class IndexLibraryPathsCommandHandler : IRequestHandler<IndexLibraryPathsCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileIndexer _fileIndexerService;
    private readonly ILibraryNotifier _libraryNotifier;
    private readonly IMediaQueryCacheInvalidator _cacheInvalidator;

    public IndexLibraryPathsCommandHandler(
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

    public async Task Handle(IndexLibraryPathsCommand request, CancellationToken cancellationToken)
    {
        var entity = await _context.Libraries
            .FirstOrDefaultAsync(x => x.Id == request.LibraryId, cancellationToken);

        Guard.Against.NotFound(request.LibraryId, entity);

        var result = await _fileIndexerService.IndexPathsAsync(entity, request.Paths, cancellationToken);

        await UpdateScanIssuesAsync(entity, result, cancellationToken);
        _cacheInvalidator.InvalidateAll();
        await _libraryNotifier.NotifyLibraryScanCompletedAsync(entity.Id, result.AddedCount, result.SkippedCount, result.InaccessiblePaths.Count, cancellationToken);
    }

    private async Task UpdateScanIssuesAsync(Library entity, Domain.Models.LibraryScanResult result, CancellationToken cancellationToken)
    {
        if (result.InaccessiblePaths.Count == 0)
            return;

        await _context.ScanIssues
            .Where(s => s.LibraryId == entity.Id)
            .ExecuteDeleteAsync(cancellationToken);

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
        await _context.SaveChangesAsync(cancellationToken);
    }
}
