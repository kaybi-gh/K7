using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Features.MetadataPictures.Services;

public sealed class MetadataPictureDeletionService(
    IApplicationDbContext context,
    ILogger<MetadataPictureDeletionService> logger)
{
    public async Task RemoveMediaPicturesByTypeAsync(
        Guid mediaId,
        MetadataPictureType pictureType,
        CancellationToken cancellationToken = default)
    {
        var pictures = await context.MetadataPictures
            .Include(p => p.Variants)
            .Where(p => p.MediaId == mediaId && p.Type == pictureType)
            .ToListAsync(cancellationToken);

        foreach (var picture in pictures)
            Remove(picture);
    }

    public async Task RemovePersonPortraitAsync(
        Guid personId,
        CancellationToken cancellationToken = default)
    {
        var pictures = await context.MetadataPictures
            .Include(p => p.Variants)
            .Where(p => p.PersonId == personId)
            .ToListAsync(cancellationToken);

        foreach (var picture in pictures)
            Remove(picture);
    }

    public void Remove(MetadataPicture picture)
    {
        DeleteFiles(picture);
        context.MetadataPictures.Remove(picture);
        logger.LogDebug("Removed metadata picture {PictureId} ({Type})", picture.Id, picture.Type);
    }

    private static void DeleteFiles(MetadataPicture picture)
    {
        foreach (var variant in picture.Variants)
        {
            if (File.Exists(variant.LocalPath))
                File.Delete(variant.LocalPath);
        }

        if (picture.LocalPath is not null && File.Exists(picture.LocalPath))
            File.Delete(picture.LocalPath);
    }
}
