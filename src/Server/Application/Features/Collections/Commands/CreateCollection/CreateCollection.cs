using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Collections.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Shared.Dtos.Rules;

namespace K7.Server.Application.Features.Collections.Commands.CreateCollection;

public static class AllowedCollectionMediaTypes
{
    public static readonly MediaType[] Values =
    [
        MediaType.Movie,
        MediaType.MusicAlbum,
        MediaType.MusicTrack,
        MediaType.MusicArtist,
        MediaType.Serie,
        MediaType.SerieSeason,
        MediaType.SerieEpisode
    ];
}

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record CreateCollectionCommand : IRequest<Guid>
{
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

public class CreateCollectionCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<CreateCollectionCommand, Guid>
{
    public async Task<Guid> Handle(CreateCollectionCommand request, CancellationToken cancellationToken)
    {
        if (request.MediaType.HasValue && !AllowedCollectionMediaTypes.Values.Contains(request.MediaType.Value))
            throw new ValidationException($"MediaType must be one of: {string.Join(", ", AllowedCollectionMediaTypes.Values)}");

        Guid? libraryGroupId = null;
        if (request.LibraryGroupId is { } groupId)
        {
            if (request.RuleFilter is null)
                throw new ValidationException("LibraryGroupId is only valid for dynamic collections.");

            var groupExists = await context.LibraryGroups.AsNoTracking()
                .AnyAsync(g => g.Id == groupId, cancellationToken);
            if (!groupExists)
                throw new NotFoundException(groupId.ToString(), nameof(Domain.Entities.LibraryGroup));

            libraryGroupId = groupId;
        }

        var visibilityScope = request.VisibilityScope != VisibilityScope.Nobody
            ? request.VisibilityScope
            : request.IsPublic ? VisibilityScope.LocalServer : VisibilityScope.Nobody;

        var entity = new Collection
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            IsPublic = visibilityScope is VisibilityScope.LocalServer or VisibilityScope.Federation,
            VisibilityScope = visibilityScope,
            MediaType = request.MediaType,
            LibraryGroupId = libraryGroupId,
            UserId = currentUser.Id!.Value,
            RuleFilter = request.RuleFilter?.ToRuleGroup(),
            Limit = request.Limit,
            OrderBy = request.OrderBy,
            OrderDescending = request.OrderDescending
        };

        entity.AddDomainEvent(new CollectionCreatedEvent(entity));
        context.Collections.Add(entity);

        if (entity.RuleFilter is not null)
            await CollectionEvaluator.RebuildItemsAsync(context, entity, currentUser.Id!.Value, cancellationToken);

        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
