using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Application.Features.Users.Commands.UpdateUserMediaExclusions;

[Authorize(Roles = Roles.Administrator)]
public record UpdateUserMediaExclusionsCommand : IRequest
{
    public required Guid Id { get; init; }
    public required IReadOnlyList<Guid> ExcludedMediaIds { get; init; }
}

public class UpdateUserMediaExclusionsCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateUserMediaExclusionsCommand>
{
    public async Task Handle(UpdateUserMediaExclusionsCommand request, CancellationToken cancellationToken)
    {
        var existing = await context.UserMediaExclusions
            .Where(e => e.UserId == request.Id)
            .ToListAsync(cancellationToken);

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, user);

        context.UserMediaExclusions.RemoveRange(existing);

        context.UserMediaExclusions.AddRange(request.ExcludedMediaIds.Select(mediaId => new UserMediaExclusion
        {
            Id = Guid.NewGuid(),
            UserId = request.Id,
            MediaId = mediaId
        }));

        await context.SaveChangesAsync(cancellationToken);
    }
}
