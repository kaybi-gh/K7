using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Clears Restrict FK dependents before deleting an orphan media. ExternalIds and
/// MetadataPictures are not ON DELETE CASCADE, so a bare DELETE FROM Medias fails on SQLite.
/// </summary>
public static class MediaOrphanDependentCleanupHelper
{
    public static async Task<bool> HasRemainingFilesAsync(
        IApplicationDbContext context,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        return await context.IndexedFiles.AnyAsync(f => f.MediaId == mediaId, cancellationToken)
            || await context.RemoteIndexedFiles.AnyAsync(f => f.MediaId == mediaId, cancellationToken);
    }

    public static async Task ClearNonUserDependentsAsync(
        IApplicationDbContext context,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        var externalIds = await context.ExternalIds
            .Where(e => e.MediaId == mediaId)
            .ToListAsync(cancellationToken);
        context.ExternalIds.RemoveRange(externalIds);

        var pictures = await context.MetadataPictures
            .Where(p => p.MediaId == mediaId)
            .ToListAsync(cancellationToken);
        context.MetadataPictures.RemoveRange(pictures);

        var availabilities = await context.MediaLibraryAvailabilities
            .Where(a => a.MediaId == mediaId)
            .ToListAsync(cancellationToken);
        context.MediaLibraryAvailabilities.RemoveRange(availabilities);

        var itemBookmarks = await context.PlaybackBookmarks
            .OfType<ItemPlaybackBookmark>()
            .Where(b => b.MediaId == mediaId)
            .ToListAsync(cancellationToken);
        context.PlaybackBookmarks.RemoveRange(itemBookmarks);
    }
}
