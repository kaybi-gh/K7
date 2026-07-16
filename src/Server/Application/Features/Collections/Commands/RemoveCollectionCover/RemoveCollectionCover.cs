using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Collections.Commands.RemoveCollectionCover;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record RemoveCollectionCoverCommand(Guid CollectionId) : IRequest;

public class RemoveCollectionCoverCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<RemoveCollectionCoverCommand>
{
    public async Task Handle(RemoveCollectionCoverCommand request, CancellationToken cancellationToken)
    {
        var collection = await context.Collections
            .Include(c => c.CoverPicture)
            .FirstOrDefaultAsync(c => c.Id == request.CollectionId && c.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.CollectionId, collection);

        if (collection.CoverPicture is null)
            return;

        context.MetadataPictures.Remove(collection.CoverPicture);
        await context.SaveChangesAsync(cancellationToken);
    }
}
