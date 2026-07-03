using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Federation.Commands.CreateVirtualUser;

[Authorize(Roles = Roles.Administrator)]
public record CreateVirtualUserCommand : IRequest<Guid>
{
    public required Guid PeerServerId { get; init; }
    public required Guid OriginUserId { get; init; }
    public required string DisplayName { get; init; }
}

public class CreateVirtualUserCommandHandler(IApplicationDbContext context)
    : IRequestHandler<CreateVirtualUserCommand, Guid>
{
    public async Task<Guid> Handle(CreateVirtualUserCommand request, CancellationToken cancellationToken)
    {
        var peer = await context.PeerServers
            .FirstOrDefaultAsync(p => p.Id == request.PeerServerId && p.Status == PeerStatus.Active, cancellationToken);

        Guard.Against.NotFound(request.PeerServerId, peer);

        var existing = await context.Users
            .FirstOrDefaultAsync(u => u.PeerServerId == request.PeerServerId
                && u.OriginUserId == request.OriginUserId, cancellationToken);

        if (existing is not null)
        {
            if (existing.DisplayName != request.DisplayName)
            {
                existing.DisplayName = request.DisplayName;
                await context.SaveChangesAsync(cancellationToken);
            }

            return existing.Id;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            PeerServerId = request.PeerServerId,
            OriginUserId = request.OriginUserId,
            DisplayName = request.DisplayName,
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync(cancellationToken);

        return user.Id;
    }
}
