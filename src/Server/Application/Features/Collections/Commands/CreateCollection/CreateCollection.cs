using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

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
}

public class CreateCollectionCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<CreateCollectionCommand, Guid>
{
    public async Task<Guid> Handle(CreateCollectionCommand request, CancellationToken cancellationToken)
    {
        if (request.MediaType.HasValue && !AllowedCollectionMediaTypes.Values.Contains(request.MediaType.Value))
            throw new ValidationException($"MediaType must be one of: {string.Join(", ", AllowedCollectionMediaTypes.Values)}");

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
            UserId = currentUser.Id!.Value
        };

        entity.AddDomainEvent(new CollectionCreatedEvent(entity));
        context.Collections.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
