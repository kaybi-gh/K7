using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Collections.Commands.DeleteCollection;

public record DeleteCollectionCommand(Guid Id) : IRequest;

public class DeleteCollectionCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<DeleteCollectionCommand>
{
    public async Task Handle(DeleteCollectionCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.Collections
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        context.Collections.Remove(entity);
        entity.AddDomainEvent(new CollectionDeletedEvent(entity));
        await context.SaveChangesAsync(cancellationToken);
    }
}
