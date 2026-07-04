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
        var mediaId = await ResolveMediaIdAsync(metadataPictureId, cancellationToken);
        if (mediaId is null)
            return;

        logger.LogDebug("Broadcasting MediaPicturesUpdated for media {MediaId} (picture {PictureId})", mediaId, metadataPictureId);
        await libraryNotifier.NotifyMediaPicturesUpdatedAsync(mediaId.Value, cancellationToken);
    }

    private async Task<Guid?> ResolveMediaIdAsync(Guid metadataPictureId, CancellationToken cancellationToken)
    {
        var picture = await context.MetadataPictures
            .AsNoTracking()
            .Where(p => p.Id == metadataPictureId)
            .Select(p => new { p.MediaId, p.PersonRoleId })
            .FirstOrDefaultAsync(cancellationToken);

        if (picture is null)
            return null;

        if (picture.MediaId is not null)
            return picture.MediaId;

        if (picture.PersonRoleId is null)
            return null;

        return await context.PersonRoles
            .AsNoTracking()
            .Where(r => r.Id == picture.PersonRoleId)
            .Select(r => (Guid?)r.MediaId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
