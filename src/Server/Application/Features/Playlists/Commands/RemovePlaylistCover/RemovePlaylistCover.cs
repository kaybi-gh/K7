using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.MetadataPictures.Services;

namespace K7.Server.Application.Features.Playlists.Commands.RemovePlaylistCover;

public record RemovePlaylistCoverCommand(Guid PlaylistId) : IRequest;

public class RemovePlaylistCoverCommandHandler(
    IApplicationDbContext context,
    ICoverPictureUploadService coverUpload,
    IUser currentUser)
    : IRequestHandler<RemovePlaylistCoverCommand>
{
    public async Task Handle(RemovePlaylistCoverCommand request, CancellationToken cancellationToken)
    {
        var playlist = await context.Playlists
            .Include(p => p.CoverPicture)
                .ThenInclude(p => p!.Variants)
            .FirstOrDefaultAsync(p => p.Id == request.PlaylistId && p.UserId == currentUser.Id!.Value, cancellationToken);

        Guard.Against.NotFound(request.PlaylistId, playlist);

        if (playlist.CoverPicture is null)
            return;

        coverUpload.RemoveExistingCover(playlist.CoverPicture, "playlists", playlist.Id);
        await context.SaveChangesAsync(cancellationToken);
    }
}
