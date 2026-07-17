using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.SharedProfiles;

namespace K7.Server.Application.Features.SharedProfiles.Commands.SharePlaylistToSharedProfile;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record SharePlaylistToSharedProfileCommand : IRequest
{
    public required Guid SharedProfileId { get; init; }
    public required Guid PlaylistId { get; init; }
}

public class SharePlaylistToSharedProfileCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService)
    : IRequestHandler<SharePlaylistToSharedProfileCommand>
{
    public async Task Handle(SharePlaylistToSharedProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await SharedProfileMemberValidator.GetGroupForHostAsync(
            context, identityService, request.SharedProfileId, userId, currentUser.IdentityId, cancellationToken);

        var playlist = await context.Playlists
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PlaylistId && p.UserId == userId, cancellationToken);
        Guard.Against.NotFound(request.PlaylistId, playlist);

        var alreadyShared = await context.SharedProfilePlaylists
            .AnyAsync(
                sp => sp.SharedProfileId == request.SharedProfileId && sp.PlaylistId == request.PlaylistId,
                cancellationToken);
        if (alreadyShared)
            return;

        context.SharedProfilePlaylists.Add(new SharedProfilePlaylist
        {
            SharedProfileId = request.SharedProfileId,
            PlaylistId = request.PlaylistId
        });
        await context.SaveChangesAsync(cancellationToken);
    }
}
