using K7.Server.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.MetadataPictures.Services;

public sealed class MediaPictureReadyNotifier(
    IApplicationDbContext context,
    ILibraryNotifier libraryNotifier,
    ILogger<MediaPictureReadyNotifier> logger)
{
    public async Task NotifyIfMediaPictureReadyAsync(Guid metadataPictureId, CancellationToken cancellationToken = default)
    {
        var picture = await context.MetadataPictures
            .AsNoTracking()
            .Where(p => p.Id == metadataPictureId)
            .Select(p => new { p.MediaId, p.PersonId, p.PersonRoleId })
            .FirstOrDefaultAsync(cancellationToken);

        if (picture is null)
            return;

        if (picture.MediaId is Guid mediaId)
        {
            logger.LogDebug("Broadcasting MediaPicturesUpdated for media {MediaId} (picture {PictureId})", mediaId, metadataPictureId);
            await libraryNotifier.NotifyMediaPicturesUpdatedAsync(mediaId, cancellationToken);
            return;
        }

        if (picture.PersonId is Guid personId)
        {
            logger.LogDebug("Broadcasting PersonPicturesUpdated for person {PersonId} (picture {PictureId})", personId, metadataPictureId);
            await libraryNotifier.NotifyPersonPicturesUpdatedAsync(personId, cancellationToken);
            return;
        }

        if (picture.PersonRoleId is null)
            return;

        var roleMediaId = await context.PersonRoles
            .AsNoTracking()
            .Where(r => r.Id == picture.PersonRoleId)
            .Select(r => (Guid?)r.MediaId)
            .FirstOrDefaultAsync(cancellationToken);

        if (roleMediaId is null)
            return;

        logger.LogDebug("Broadcasting MediaPicturesUpdated for media {MediaId} (picture {PictureId})", roleMediaId, metadataPictureId);
        await libraryNotifier.NotifyMediaPicturesUpdatedAsync(roleMediaId.Value, cancellationToken);
    }
}
