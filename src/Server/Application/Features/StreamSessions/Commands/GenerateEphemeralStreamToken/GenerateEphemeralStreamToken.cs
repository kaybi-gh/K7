using System.Security.Cryptography;
using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Features.StreamSessions.Commands.GenerateEphemeralStreamToken;

public record GenerateEphemeralStreamTokenCommand : IRequest<string>
{
    public required Guid StreamSessionId { get; init; }
}

public class GenerateEphemeralStreamTokenCommandHandler : IRequestHandler<GenerateEphemeralStreamTokenCommand, string>
{
    private readonly IApplicationDbContext _context;
    private readonly IUser _user;

    public GenerateEphemeralStreamTokenCommandHandler(IApplicationDbContext context, IUser user)
    {
        _context = context;
        _user = user;
    }

    public async Task<string> Handle(GenerateEphemeralStreamTokenCommand request, CancellationToken cancellationToken)
    {
        var session = await _context.StreamSessions.FindAsync([request.StreamSessionId], cancellationToken);
        Guard.Against.NotFound(request.StreamSessionId, session);

        if (session.UserId != _user.Id)
        {
            throw new ForbiddenAccessException();
        }

        var token = GenerateToken();

        var entity = new EphemeralStreamToken
        {
            Id = Guid.NewGuid(),
            Token = token,
            StreamSessionId = request.StreamSessionId,
            UserId = _user.Id!.Value,
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(8),
            IsRevoked = false,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _context.EphemeralStreamTokens.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return token;
    }

    private static string GenerateToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
    }
}
