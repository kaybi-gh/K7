using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Collections.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Rules;

namespace K7.Server.Application.Features.Collections.Commands.UpdateCollection;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record UpdateCollectionCommand : IRequest
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public bool IsPublic { get; init; }
    public VisibilityScope VisibilityScope { get; init; } = VisibilityScope.Nobody;
    public MediaType? MediaType { get; init; }
    public Guid? LibraryGroupId { get; init; }
    public RuleGroupDto? RuleFilter { get; init; }
    public int? Limit { get; init; }
    public DynamicPlaylistOrderBy OrderBy { get; init; } = DynamicPlaylistOrderBy.DateAdded;
    public bool OrderDescending { get; init; } = true;
}

public class UpdateCollectionCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<UpdateCollectionCommand>
{
    public async Task Handle(UpdateCollectionCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.Collections
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        var visibilityScope = request.VisibilityScope != VisibilityScope.Nobody
            ? request.VisibilityScope
            : request.IsPublic ? VisibilityScope.LocalServer : VisibilityScope.Nobody;

        entity.Title = request.Title;
        entity.Description = request.Description;
        entity.IsPublic = visibilityScope is VisibilityScope.LocalServer or VisibilityScope.Federation;
        entity.VisibilityScope = visibilityScope;

        if (request.RuleFilter is not null)
        {
            Guid? libraryGroupId = null;
            if (request.LibraryGroupId is { } groupId)
            {
                var groupExists = await context.LibraryGroups.AsNoTracking()
                    .AnyAsync(g => g.Id == groupId, cancellationToken);
                if (!groupExists)
                    throw new NotFoundException(groupId.ToString(), nameof(Domain.Entities.LibraryGroup));

                libraryGroupId = groupId;
            }

            entity.LibraryGroupId = libraryGroupId;
            entity.RuleFilter = request.RuleFilter.ToRuleGroup();
            entity.Limit = request.Limit;
            entity.OrderBy = request.OrderBy;
            entity.OrderDescending = request.OrderDescending;
            if (request.MediaType.HasValue)
                entity.MediaType = request.MediaType;

            await CollectionEvaluator.RebuildItemsAsync(context, entity, currentUser.Id!.Value, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
