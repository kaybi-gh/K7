using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Features.Libraries.Queries.GetLibraryStatistics;

[Authorize(Roles = Roles.Administrator)]
public record GetLibraryStatisticsQuery : IRequest<List<LibraryStatisticsDto>>;

public class GetLibraryStatisticsQueryHandler : IRequestHandler<GetLibraryStatisticsQuery, List<LibraryStatisticsDto>>
{
    private readonly IApplicationDbContext _context;

    public GetLibraryStatisticsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<LibraryStatisticsDto>> Handle(GetLibraryStatisticsQuery request, CancellationToken cancellationToken)
    {
        var mediaCounts = await _context.IndexedFiles
            .Where(f => f.MediaId != null)
            .Join(
                _context.Medias,
                f => f.MediaId,
                m => m.Id,
                (f, m) => new { f.LibraryId, m.Type })
            .GroupBy(x => new { x.LibraryId, x.Type })
            .Select(g => new { g.Key.LibraryId, g.Key.Type, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var fileCounts = await _context.IndexedFiles
            .GroupBy(f => f.LibraryId)
            .Select(g => new { LibraryId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var libraryIds = await _context.Libraries
            .AsNoTracking()
            .Select(l => l.Id)
            .ToListAsync(cancellationToken);

        return libraryIds.Select(id => new LibraryStatisticsDto
        {
            LibraryId = id,
            FileCount = fileCounts.FirstOrDefault(f => f.LibraryId == id)?.Count ?? 0,
            MediaCounts = mediaCounts
                .Where(m => m.LibraryId == id)
                .ToDictionary(m => m.Type, m => m.Count)
        }).ToList();
    }
}
