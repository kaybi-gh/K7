using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Moves user-facing state from a mis-identified media onto the corrected media
/// so watch progress is not left on the wrong title after re-link / re-identify.
/// </summary>
public static class MediaUserStateTransferHelper
{
    public static async Task TransferAsync(
        IApplicationDbContext context,
        Guid fromMediaId,
        Guid toMediaId,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (fromMediaId == toMediaId || fromMediaId == Guid.Empty || toMediaId == Guid.Empty)
            return;

        var transferredStates = await TransferUserMediaStatesAsync(context, fromMediaId, toMediaId, cancellationToken);
        var transferredShared = await TransferSharedProfileMediaStatesAsync(context, fromMediaId, toMediaId, cancellationToken);
        var transferredReviews = await TransferMediaReviewsAsync(context, fromMediaId, toMediaId, cancellationToken);
        var transferredPlaylists = await TransferPlaylistItemsAsync(context, fromMediaId, toMediaId, cancellationToken);
        var transferredRatings = await TransferUserRatingsAsync(context, fromMediaId, toMediaId, cancellationToken);
        var transferredCollections = await TransferCollectionItemsAsync(context, fromMediaId, toMediaId, cancellationToken);
        var transferredSessions = await TransferPlaybackSessionsAsync(context, fromMediaId, toMediaId, cancellationToken);
        var transferredExclusions = await TransferMediaExclusionsAsync(context, fromMediaId, toMediaId, cancellationToken);

        if (transferredStates
            + transferredShared
            + transferredReviews
            + transferredPlaylists
            + transferredRatings
            + transferredCollections
            + transferredSessions
            + transferredExclusions == 0)
        {
            return;
        }

        logger.LogInformation(
            "Transferred user state from media {FromMediaId} to {ToMediaId} (states={States}, shared={Shared}, reviews={Reviews}, playlists={Playlists}, ratings={Ratings}, collections={Collections}, sessions={Sessions}, exclusions={Exclusions})",
            fromMediaId,
            toMediaId,
            transferredStates,
            transferredShared,
            transferredReviews,
            transferredPlaylists,
            transferredRatings,
            transferredCollections,
            transferredSessions,
            transferredExclusions);
    }

    private static async Task<int> TransferUserMediaStatesAsync(
        IApplicationDbContext context,
        Guid fromMediaId,
        Guid toMediaId,
        CancellationToken cancellationToken)
    {
        var sourceStates = await context.UserMediaStates
            .Where(s => s.MediaId == fromMediaId)
            .ToListAsync(cancellationToken);

        if (sourceStates.Count == 0)
            return 0;

        var userIds = sourceStates.Select(s => s.UserId).Distinct().ToList();
        var targetStates = await context.UserMediaStates
            .Where(s => s.MediaId == toMediaId && userIds.Contains(s.UserId))
            .ToDictionaryAsync(s => s.UserId, cancellationToken);

        foreach (var source in sourceStates)
        {
            if (targetStates.TryGetValue(source.UserId, out var target))
            {
                MergeWatchState(target, source);
                context.UserMediaStates.Remove(source);
            }
            else
            {
                source.MediaId = toMediaId;
            }
        }

        return sourceStates.Count;
    }

    private static async Task<int> TransferSharedProfileMediaStatesAsync(
        IApplicationDbContext context,
        Guid fromMediaId,
        Guid toMediaId,
        CancellationToken cancellationToken)
    {
        var sourceStates = await context.SharedProfileMediaStates
            .Where(s => s.MediaId == fromMediaId)
            .ToListAsync(cancellationToken);

        if (sourceStates.Count == 0)
            return 0;

        var profileIds = sourceStates.Select(s => s.SharedProfileId).Distinct().ToList();
        var targetStates = await context.SharedProfileMediaStates
            .Where(s => s.MediaId == toMediaId && profileIds.Contains(s.SharedProfileId))
            .ToDictionaryAsync(s => s.SharedProfileId, cancellationToken);

        foreach (var source in sourceStates)
        {
            if (targetStates.TryGetValue(source.SharedProfileId, out var target))
            {
                MergeSharedWatchState(target, source);
                context.SharedProfileMediaStates.Remove(source);
            }
            else
            {
                source.MediaId = toMediaId;
            }
        }

        return sourceStates.Count;
    }

    private static async Task<int> TransferMediaReviewsAsync(
        IApplicationDbContext context,
        Guid fromMediaId,
        Guid toMediaId,
        CancellationToken cancellationToken)
    {
        var sourceReviews = await context.MediaReviews
            .Where(r => r.MediaId == fromMediaId)
            .ToListAsync(cancellationToken);

        if (sourceReviews.Count == 0)
            return 0;

        var userIds = sourceReviews.Select(r => r.UserId).Distinct().ToList();
        var targetReviews = await context.MediaReviews
            .Where(r => r.MediaId == toMediaId && userIds.Contains(r.UserId))
            .ToDictionaryAsync(r => r.UserId, cancellationToken);

        foreach (var source in sourceReviews)
        {
            if (targetReviews.ContainsKey(source.UserId))
            {
                // Keep the review already on the correct media; drop the mis-linked one.
                context.MediaReviews.Remove(source);
            }
            else
            {
                source.MediaId = toMediaId;
            }
        }

        return sourceReviews.Count;
    }

    private static async Task<int> TransferPlaylistItemsAsync(
        IApplicationDbContext context,
        Guid fromMediaId,
        Guid toMediaId,
        CancellationToken cancellationToken)
    {
        var sourceItems = await context.PlaylistItems
            .Where(p => p.MediaId == fromMediaId)
            .ToListAsync(cancellationToken);

        if (sourceItems.Count == 0)
            return 0;

        var playlistIds = sourceItems.Select(p => p.PlaylistId).Distinct().ToList();
        var targetPlaylistIds = await context.PlaylistItems
            .Where(p => p.MediaId == toMediaId && playlistIds.Contains(p.PlaylistId))
            .Select(p => p.PlaylistId)
            .ToListAsync(cancellationToken);
        var targetSet = targetPlaylistIds.ToHashSet();

        foreach (var source in sourceItems)
        {
            if (targetSet.Contains(source.PlaylistId))
                context.PlaylistItems.Remove(source);
            else
                source.MediaId = toMediaId;
        }

        return sourceItems.Count;
    }

    private static async Task<int> TransferUserRatingsAsync(
        IApplicationDbContext context,
        Guid fromMediaId,
        Guid toMediaId,
        CancellationToken cancellationToken)
    {
        var sourceRatings = await context.Ratings
            .OfType<UserRating>()
            .Where(r => r.MediaId == fromMediaId)
            .ToListAsync(cancellationToken);

        if (sourceRatings.Count == 0)
            return 0;

        var userIds = sourceRatings.Select(r => r.UserId).Distinct().ToList();
        var targetRatings = await context.Ratings
            .OfType<UserRating>()
            .Where(r => r.MediaId == toMediaId && userIds.Contains(r.UserId))
            .ToDictionaryAsync(r => r.UserId, cancellationToken);

        foreach (var source in sourceRatings)
        {
            if (targetRatings.ContainsKey(source.UserId))
                context.Ratings.Remove(source);
            else
                source.MediaId = toMediaId;
        }

        return sourceRatings.Count;
    }

    private static async Task<int> TransferCollectionItemsAsync(
        IApplicationDbContext context,
        Guid fromMediaId,
        Guid toMediaId,
        CancellationToken cancellationToken)
    {
        var sourceItems = await context.CollectionItems
            .Where(c => c.MediaId == fromMediaId)
            .ToListAsync(cancellationToken);

        if (sourceItems.Count == 0)
            return 0;

        var collectionIds = sourceItems.Select(c => c.CollectionId).Distinct().ToList();
        var targetCollectionIds = await context.CollectionItems
            .Where(c => c.MediaId == toMediaId && collectionIds.Contains(c.CollectionId))
            .Select(c => c.CollectionId)
            .ToListAsync(cancellationToken);
        var targetSet = targetCollectionIds.ToHashSet();

        foreach (var source in sourceItems)
        {
            if (targetSet.Contains(source.CollectionId))
                context.CollectionItems.Remove(source);
            else
                source.MediaId = toMediaId;
        }

        return sourceItems.Count;
    }

    private static async Task<int> TransferPlaybackSessionsAsync(
        IApplicationDbContext context,
        Guid fromMediaId,
        Guid toMediaId,
        CancellationToken cancellationToken)
    {
        var sourceSessions = await context.MediaPlaybackSessions
            .Where(s => s.MediaId == fromMediaId)
            .ToListAsync(cancellationToken);

        if (sourceSessions.Count == 0)
            return 0;

        foreach (var source in sourceSessions)
            source.MediaId = toMediaId;

        return sourceSessions.Count;
    }

    private static async Task<int> TransferMediaExclusionsAsync(
        IApplicationDbContext context,
        Guid fromMediaId,
        Guid toMediaId,
        CancellationToken cancellationToken)
    {
        var sourceExclusions = await context.UserMediaExclusions
            .Where(e => e.MediaId == fromMediaId)
            .ToListAsync(cancellationToken);

        if (sourceExclusions.Count == 0)
            return 0;

        var userIds = sourceExclusions.Select(e => e.UserId).Distinct().ToList();
        var targetExclusions = await context.UserMediaExclusions
            .Where(e => e.MediaId == toMediaId && userIds.Contains(e.UserId))
            .ToDictionaryAsync(e => e.UserId, cancellationToken);

        foreach (var source in sourceExclusions)
        {
            if (targetExclusions.TryGetValue(source.UserId, out var target))
            {
                target.IsAdminExcluded = target.IsAdminExcluded || source.IsAdminExcluded;
                target.IsSelfExcluded = target.IsSelfExcluded || source.IsSelfExcluded;
                context.UserMediaExclusions.Remove(source);
            }
            else
            {
                source.MediaId = toMediaId;
            }
        }

        return sourceExclusions.Count;
    }

    private static void MergeWatchState(UserMediaState target, UserMediaState source)
    {
        var sourceIsNewer = source.LastInteractedAt.HasValue
            && (target.LastInteractedAt is null
                || source.LastInteractedAt.Value > target.LastInteractedAt.Value);

        if (sourceIsNewer)
        {
            target.LastPlaybackPosition = source.LastPlaybackPosition;
            target.ProgressPercentage = source.ProgressPercentage;
            target.IsCompleted = source.IsCompleted;
            target.LastInteractedAt = source.LastInteractedAt;
            target.LastKnownDurationSeconds = source.LastKnownDurationSeconds;
            target.ExcludedFromContinueWatching = source.ExcludedFromContinueWatching;
        }
        else
        {
            target.IsCompleted = target.IsCompleted || source.IsCompleted;
        }

        target.PlayCount += source.PlayCount;
        target.SkipCount += source.SkipCount;
    }

    private static void MergeSharedWatchState(SharedProfileMediaState target, SharedProfileMediaState source)
    {
        var sourceIsNewer = source.LastInteractedAt.HasValue
            && (target.LastInteractedAt is null
                || source.LastInteractedAt.Value > target.LastInteractedAt.Value);

        if (sourceIsNewer)
        {
            target.LastPlaybackPosition = source.LastPlaybackPosition;
            target.ProgressPercentage = source.ProgressPercentage;
            target.IsCompleted = source.IsCompleted;
            target.LastInteractedAt = source.LastInteractedAt;
            target.LastKnownDurationSeconds = source.LastKnownDurationSeconds;
            target.ExcludedFromContinueWatching = source.ExcludedFromContinueWatching;
        }
        else
        {
            target.IsCompleted = target.IsCompleted || source.IsCompleted;
        }

        target.PlayCount += source.PlayCount;
        target.SkipCount += source.SkipCount;
    }
}
