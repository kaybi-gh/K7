using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos.Federation;

namespace K7.Server.Application.Features.Federation.Queries.GetFederationHlsSegments;

public record GetFederationHlsSegmentsQuery(string? ClientId, Guid SessionId)
    : IRequest<IReadOnlyList<HlsSegmentDto>>;

public class GetFederationHlsSegmentsQueryHandler(
    IPeerAuthorizationService peerAuthorization,
    IApplicationDbContext context)
    : IRequestHandler<GetFederationHlsSegmentsQuery, IReadOnlyList<HlsSegmentDto>>
{
    public async Task<IReadOnlyList<HlsSegmentDto>> Handle(
        GetFederationHlsSegmentsQuery request,
        CancellationToken cancellationToken)
    {
        var peer = await peerAuthorization.RequireInboundPeerAsync(request.ClientId, cancellationToken);

        var session = await context.StreamSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SessionId && s.PeerServerId == peer.Id, cancellationToken);

        if (session?.IndexedFileId is null)
            throw new NotFoundException(request.SessionId.ToString(), "StreamSession");

        var segments = await context.HlsSegments
            .AsNoTracking()
            .Where(s => s.IndexedFileId == session.IndexedFileId.Value)
            .OrderBy(s => s.Number)
            .Select(s => new HlsSegmentDto
            {
                Number = s.Number,
                StartTimestamp = s.StartTimestamp,
                Duration = s.Duration
            })
            .ToListAsync(cancellationToken);

        return segments;
    }
}
