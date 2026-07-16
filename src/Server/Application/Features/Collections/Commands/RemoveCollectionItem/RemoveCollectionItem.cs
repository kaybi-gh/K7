using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Collections.Commands.RemoveCollectionItem;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record RemoveCollectionItemCommand : IRequest
{
    public required Guid CollectionId { get; init; }
    public required Guid ItemId { get; init; }
}

public class RemoveCollectionItemCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<RemoveCollectionItemCommand>
{
    public async Task Handle(RemoveCollectionItemCommand request, CancellationToken cancellationToken)
    {
        var collection = await context.Collections
            .FirstOrDefaultAsync(c => c.Id == request.CollectionId && c.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.CollectionId, collection);

        var item = await context.CollectionItems
            .FirstOrDefaultAsync(i => i.Id == request.ItemId && i.CollectionId == request.CollectionId, cancellationToken);

        Guard.Against.NotFound(request.ItemId, item);

        context.CollectionItems.Remove(item);
        await context.SaveChangesAsync(cancellationToken);
    }
}
