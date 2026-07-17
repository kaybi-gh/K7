using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Features.SharedProfiles.Commands.UnsharePlaylistFromSharedProfile;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record UnsharePlaylistFromSharedProfileCommand : IRequest
{
    public required Guid SharedProfileId { get; init; }
    public required Guid PlaylistId { get; init; }
}

public class UnsharePlaylistFromSharedProfileCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService)
    : IRequestHandler<UnsharePlaylistFromSharedProfileCommand>
{
    public async Task Handle(UnsharePlaylistFromSharedProfileCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await SharedProfileMemberValidator.GetGroupForHostAsync(
            context, identityService, request.SharedProfileId, userId, currentUser.IdentityId, cancellationToken);

        var link = await context.SharedProfilePlaylists
            .FirstOrDefaultAsync(
                sp => sp.SharedProfileId == request.SharedProfileId && sp.PlaylistId == request.PlaylistId,
                cancellationToken);
        if (link is null)
            return;

        context.SharedProfilePlaylists.Remove(link);
        await context.SaveChangesAsync(cancellationToken);
    }
}
