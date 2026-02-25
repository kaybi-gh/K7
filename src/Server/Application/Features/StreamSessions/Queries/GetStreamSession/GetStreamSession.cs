using K7.Server.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.StreamSessions.Queries.GetStreamSession;

public record StreamSessionInfo
{
    public required Guid SessionId { get; init; }
    public required string RootDirectory { get; init; }
}

public record GetStreamSessionQuery(Guid SessionId) : IRequest<StreamSessionInfo?>;

public class GetStreamSessionQueryHandler : IRequestHandler<GetStreamSessionQuery, StreamSessionInfo?>
{
    private readonly IApplicationDbContext _context;

    public GetStreamSessionQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<StreamSessionInfo?> Handle(GetStreamSessionQuery request, CancellationToken cancellationToken)
    {
        var session = await _context.StreamSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.SessionId, cancellationToken);

        if (session is null)
        {
            return null;
        }

        return new StreamSessionInfo
        {
            SessionId = session.Id,
            RootDirectory = session.RootDirectory
        };
    }
}
