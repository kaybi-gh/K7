using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.IndexedFiles.Queries.GetHlsSegments;

public record GetHlsSegmentsQuery : IRequest<IReadOnlyList<HlsSegment>>
{
    public required Guid IndexedFileId { get; init; }
}

public class GetHlsSegmentsQueryHandler : IRequestHandler<GetHlsSegmentsQuery, IReadOnlyList<HlsSegment>>
{
    private readonly IApplicationDbContext _context;

    public GetHlsSegmentsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<HlsSegment>> Handle(GetHlsSegmentsQuery request, CancellationToken cancellationToken)
    {
        var segments = await _context.HlsSegments
            .Where(x => x.IndexedFileId == request.IndexedFileId)
            .OrderBy(x => x.Number)
            .ToListAsync(cancellationToken);

        return segments;
    }
}
