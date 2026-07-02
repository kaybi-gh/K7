using K7.Server.Application.Common.Helpers;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;

namespace K7.Server.Application.Features.Playlists.Commands.RecordPlaylistPlayback;

[Authorize]
public record RecordPlaylistPlaybackCommand(Guid PlaylistId) : IRequest;

public class RecordPlaylistPlaybackCommandHandler(IApplicationDbContext context, IUser currentUser)
    : IRequestHandler<RecordPlaylistPlaybackCommand>
{
    public async Task Handle(RecordPlaylistPlaybackCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return;

        await UserPlaylistStateHelper.TouchLastListenedAsync(context, userId, request.PlaylistId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }
}
