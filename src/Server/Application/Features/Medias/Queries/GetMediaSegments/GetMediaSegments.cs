using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities.Medias;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Server.Application.Features.Medias.Queries.GetMediaSegments;

public record GetMediaSegmentsQuery(Guid MediaId) : IRequest<IReadOnlyList<MediaSegmentDto>>;

public class GetMediaSegmentsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetMediaSegmentsQuery, IReadOnlyList<MediaSegmentDto>>
{
    public async Task<IReadOnlyList<MediaSegmentDto>> Handle(GetMediaSegmentsQuery request, CancellationToken cancellationToken)
    {
        var segments = await context.MediaSegments
            .AsNoTracking()
            .Where(s => s.MediaId == request.MediaId)
            .OrderBy(s => s.StartMs)
            .ToListAsync(cancellationToken);

        return segments.Select(s => s.ToMediaSegmentDto()).ToList();
    }
}
