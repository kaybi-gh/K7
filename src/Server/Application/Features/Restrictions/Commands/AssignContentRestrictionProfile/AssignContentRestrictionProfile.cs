using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Restrictions.Commands.AssignContentRestrictionProfile;

[Authorize(Roles = Roles.Administrator)]
public record AssignContentRestrictionProfileCommand : IRequest
{
    public required Guid UserId { get; init; }
    public Guid? ProfileId { get; init; }
}

public class AssignContentRestrictionProfileCommandHandler(IApplicationDbContext context)
    : IRequestHandler<AssignContentRestrictionProfileCommand>
{
    public async Task Handle(AssignContentRestrictionProfileCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        Guard.Against.NotFound(request.UserId, user);

        if (request.ProfileId.HasValue)
        {
            var profile = await context.ContentRestrictionProfiles
                .FirstOrDefaultAsync(p => p.Id == request.ProfileId.Value, cancellationToken);
            Guard.Against.NotFound(request.ProfileId.Value, profile);
        }

        user.ContentRestrictionProfileId = request.ProfileId;
        await context.SaveChangesAsync(cancellationToken);
    }
}
