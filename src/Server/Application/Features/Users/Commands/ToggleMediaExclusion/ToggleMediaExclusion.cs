using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Features.Users.Commands.ToggleMediaExclusion;

public record ToggleMediaExclusionCommand : IRequest<bool>
{
    public required Guid MediaId { get; init; }
}

public class ToggleMediaExclusionCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<ToggleMediaExclusionCommand, bool>
{
    public async Task<bool> Handle(ToggleMediaExclusionCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);

        var existing = await context.UserMediaExclusions
            .FirstOrDefaultAsync(e => e.UserId == userId && e.MediaId == request.MediaId, cancellationToken);

        if (existing is not null)
        {
            context.UserMediaExclusions.Remove(existing);
            await context.SaveChangesAsync(cancellationToken);
            return false;
        }

        context.UserMediaExclusions.Add(new UserMediaExclusion
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MediaId = request.MediaId
        });

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
