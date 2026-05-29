using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.StreamSessions.Commands.RevokeEphemeralStreamToken;

public record RevokeEphemeralStreamTokenCommand : IRequest
{
    public required Guid StreamSessionId { get; init; }
}

public class RevokeEphemeralStreamTokenCommandHandler : IRequestHandler<RevokeEphemeralStreamTokenCommand>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public RevokeEphemeralStreamTokenCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task Handle(RevokeEphemeralStreamTokenCommand request, CancellationToken cancellationToken)
    {
        var tokens = await _context.EphemeralStreamTokens
            .Where(t => t.StreamSessionId == request.StreamSessionId && t.UserId == _user.Id && !t.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
        {
            token.IsRevoked = true;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }
}
