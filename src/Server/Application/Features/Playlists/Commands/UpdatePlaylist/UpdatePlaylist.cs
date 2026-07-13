using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;

namespace K7.Server.Application.Features.Playlists.Commands.UpdatePlaylist;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record UpdatePlaylistCommand : IRequest
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required MediaType MediaType { get; init; }
    public VisibilityScope VisibilityScope { get; init; } = VisibilityScope.Nobody;
}

public class UpdatePlaylistCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<UpdatePlaylistCommand>
{
    public async Task Handle(UpdatePlaylistCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.Playlists
            .FirstOrDefaultAsync(p => p.Id == request.Id && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        if (entity.MediaType != request.MediaType)
        {
            throw new Common.Exceptions.ValidationException([
                new FluentValidation.Results.ValidationFailure(
                    nameof(request.MediaType),
                    "Playlist media type cannot be changed.")
            ]);
        }

        entity.Title = request.Title;
        entity.Description = request.Description;
        entity.VisibilityScope = request.VisibilityScope;

        entity.AddDomainEvent(new PlaylistUpdatedEvent(entity));
        await context.SaveChangesAsync(cancellationToken);
    }
}
