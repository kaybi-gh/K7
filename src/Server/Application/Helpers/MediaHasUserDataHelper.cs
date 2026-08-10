using K7.Server.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Shared check for user/social data keyed by media id (blocks orphan deletion).
/// </summary>
public static class MediaHasUserDataHelper
{
    public static async Task<bool> HasUserDataAsync(
        IApplicationDbContext context,
        Guid mediaId,
        CancellationToken cancellationToken = default)
    {
        if (mediaId == Guid.Empty)
            return false;

        return await context.UserMediaStates.AnyAsync(s => s.MediaId == mediaId, cancellationToken)
            || await context.SharedProfileMediaStates.AnyAsync(s => s.MediaId == mediaId, cancellationToken)
            || await context.MediaReviews.AnyAsync(r => r.MediaId == mediaId, cancellationToken)
            || await context.PlaylistItems.AnyAsync(p => p.MediaId == mediaId, cancellationToken)
            || await context.CollectionItems.AnyAsync(c => c.MediaId == mediaId, cancellationToken)
            || await context.MediaPlaybackSessions.AnyAsync(s => s.MediaId == mediaId, cancellationToken)
            || await context.UserMediaExclusions.AnyAsync(e => e.MediaId == mediaId, cancellationToken);
    }
}
