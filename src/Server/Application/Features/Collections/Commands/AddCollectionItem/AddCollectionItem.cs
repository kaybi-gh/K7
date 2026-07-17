using K7.Server.Application.Common.Behaviours;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Collections.Commands.CreateCollection;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Collections.Commands.AddCollectionItem;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record AddCollectionItemCommand : IRequest<Guid>, IMediaScopedRequest
{
    public required Guid CollectionId { get; init; }
    public required Guid MediaId { get; init; }
}

public class AddCollectionItemCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<AddCollectionItemCommand, Guid>
{
    public async Task<Guid> Handle(AddCollectionItemCommand request, CancellationToken cancellationToken)
    {
        var collection = await context.Collections
            .FirstOrDefaultAsync(c => c.Id == request.CollectionId && c.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.CollectionId, collection);

        var media = await context.Medias
            .Where(m => m.Id == request.MediaId)
            .Select(m => new { m.Id, m.Type })
            .FirstOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.MediaId, media);

        if (collection.MediaType.HasValue && media.Type != collection.MediaType.Value)
            throw new ValidationException($"Cannot add a media of type {media.Type} to a collection of type {collection.MediaType.Value}.");

        if (!AllowedCollectionMediaTypes.Values.Contains(media.Type))
            throw new ValidationException($"Only media of types {string.Join(", ", AllowedCollectionMediaTypes.Values)} can be added to collections.");

        var maxOrder = await context.CollectionItems
            .Where(i => i.CollectionId == request.CollectionId)
            .Select(i => (int?)i.Order)
            .MaxAsync(cancellationToken) ?? -1;

        var item = new CollectionItem
        {
            CollectionId = collection.Id,
            MediaId = request.MediaId,
            Order = maxOrder + 1
        };

        context.CollectionItems.Add(item);
        await context.SaveChangesAsync(cancellationToken);

        return item.Id;
    }
}
