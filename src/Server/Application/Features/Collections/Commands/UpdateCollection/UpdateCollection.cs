using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Collections.Commands.UpdateCollection;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record UpdateCollectionCommand : IRequest
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public bool IsPublic { get; init; }
    public VisibilityScope VisibilityScope { get; init; } = VisibilityScope.Nobody;
}

public class UpdateCollectionCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<UpdateCollectionCommand>
{
    public async Task Handle(UpdateCollectionCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.Collections
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        var visibilityScope = request.VisibilityScope != VisibilityScope.Nobody
            ? request.VisibilityScope
            : request.IsPublic ? VisibilityScope.LocalServer : VisibilityScope.Nobody;

        entity.Title = request.Title;
        entity.Description = request.Description;
        entity.IsPublic = visibilityScope is VisibilityScope.LocalServer or VisibilityScope.Federation;
        entity.VisibilityScope = visibilityScope;

        await context.SaveChangesAsync(cancellationToken);
    }
}
