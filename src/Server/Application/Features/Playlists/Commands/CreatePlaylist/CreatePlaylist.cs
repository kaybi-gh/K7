using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Playlists.Commands.CreatePlaylist;

public static class AllowedPlaylistMediaTypes
{
    public static readonly MediaType[] Values = [MediaType.Movie, MediaType.MusicTrack, MediaType.SerieEpisode];
}

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record CreatePlaylistCommand : IRequest<Guid>
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required MediaType MediaType { get; init; }
    public VisibilityScope VisibilityScope { get; init; } = VisibilityScope.Nobody;
}

public class CreatePlaylistCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<CreatePlaylistCommand, Guid>
{
    public async Task<Guid> Handle(CreatePlaylistCommand request, CancellationToken cancellationToken)
    {
        if (!AllowedPlaylistMediaTypes.Values.Contains(request.MediaType))
            throw new ValidationException($"MediaType must be one of: {string.Join(", ", AllowedPlaylistMediaTypes.Values)}");

        var entity = new Playlist
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            MediaType = request.MediaType,
            VisibilityScope = request.VisibilityScope,
            UserId = currentUser.Id!.Value
        };

        entity.AddDomainEvent(new PlaylistCreatedEvent(entity));
        context.Playlists.Add(entity);
        await context.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }
}
