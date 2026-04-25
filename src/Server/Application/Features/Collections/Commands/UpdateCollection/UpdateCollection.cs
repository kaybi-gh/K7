using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Collections.Commands.UpdateCollection;

public record UpdateCollectionCommand : IRequest
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public bool IsPublic { get; init; }
}

public class UpdateCollectionCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<UpdateCollectionCommand>
{
    public async Task Handle(UpdateCollectionCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.Collections
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        entity.Title = request.Title;
        entity.Description = request.Description;
        entity.IsPublic = request.IsPublic;

        await context.SaveChangesAsync(cancellationToken);
    }
}
