using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Collections.Services;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Collections.Commands.EvaluateCollection;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record EvaluateCollectionCommand : IRequest<Guid>
{
    public required Guid Id { get; init; }
}

public class EvaluateCollectionCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<EvaluateCollectionCommand, Guid>
{
    public async Task<Guid> Handle(EvaluateCollectionCommand request, CancellationToken cancellationToken)
    {
        var collection = await context.Collections
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, collection);

        await CollectionEvaluator.RebuildItemsAsync(context, collection, currentUser.Id!.Value, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);

        return collection.Id;
    }
}
