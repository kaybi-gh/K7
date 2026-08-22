using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Services;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.Medias.Commands.DismissFromContinueWatching;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record DismissFromContinueWatchingCommand(Guid MediaId) : IRequest;

public class DismissFromContinueWatchingCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IMediaAccessGuard accessGuard,
    IPlaybackBookmarkService bookmarkService,
    IMediaQueryCacheInvalidator cacheInvalidator)
    : IRequestHandler<DismissFromContinueWatchingCommand>
{
    public async Task Handle(DismissFromContinueWatchingCommand request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return;

        await accessGuard.EnsureAccessAsync(request.MediaId, cancellationToken);
        await bookmarkService.DismissAsync(request.MediaId, userId, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        cacheInvalidator.InvalidateAll();
    }
}
