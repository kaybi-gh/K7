using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.SharedProfiles;

/// <summary>
/// Copies shared-profile playback progress into each member's personal <see cref="UserMediaState"/>
/// and playback bookmarks before a shared profile is deleted (cascade would otherwise drop the shared rows).
/// </summary>
internal static class SharedProfileMediaStateMigration
{
    public static async Task MigrateToMembersAsync(
        IApplicationDbContext context,
        Guid sharedProfileId,
        IReadOnlyCollection<Guid> memberUserIds,
        CancellationToken cancellationToken)
    {
        if (memberUserIds.Count == 0)
            return;

        var sharedStates = await context.SharedProfileMediaStates
            .AsNoTracking()
            .Where(s => s.SharedProfileId == sharedProfileId)
            .ToListAsync(cancellationToken);

        var sharedBookmarks = await context.PlaybackBookmarks
            .AsNoTracking()
            .Where(b => b.SharedProfileId == sharedProfileId)
            .ToListAsync(cancellationToken);

        if (sharedStates.Count == 0 && sharedBookmarks.Count == 0)
            return;

        var mediaIds = sharedStates.Select(s => s.MediaId).Distinct().ToList();
        var recipientIds = memberUserIds.Distinct().ToList();

        var existingStates = await context.UserMediaStates
            .Where(s => recipientIds.Contains(s.UserId) && mediaIds.Contains(s.MediaId))
            .ToListAsync(cancellationToken);

        var existingByUserAndMedia = existingStates
            .ToDictionary(s => (s.UserId, s.MediaId));

        foreach (var userId in recipientIds)
        {
            foreach (var shared in sharedStates)
            {
                if (existingByUserAndMedia.TryGetValue((userId, shared.MediaId), out var personal))
                {
                    MergeIntoPersonal(personal, shared);
                    continue;
                }

                var created = new UserMediaState
                {
                    UserId = userId,
                    MediaId = shared.MediaId,
                    IsCompleted = shared.IsCompleted,
                    PlayCount = shared.PlayCount,
                    SkipCount = shared.SkipCount,
                    LastInteractedAt = shared.LastInteractedAt
                };
                context.UserMediaStates.Add(created);
                existingByUserAndMedia[(userId, shared.MediaId)] = created;
            }

            foreach (var bookmark in sharedBookmarks)
            {
                if (bookmark is ItemPlaybackBookmark item)
                {
                    var exists = await context.PlaybackBookmarks
                        .OfType<ItemPlaybackBookmark>()
                        .AnyAsync(b => b.UserId == userId && b.MediaId == item.MediaId, cancellationToken);
                    if (exists)
                        continue;

                    context.PlaybackBookmarks.Add(new ItemPlaybackBookmark
                    {
                        UserId = userId,
                        MediaId = item.MediaId,
                        PositionSeconds = item.PositionSeconds,
                        DurationSeconds = item.DurationSeconds,
                        UpdatedAt = item.UpdatedAt
                    });
                }
                else if (bookmark is SeriesPlaybackBookmark series)
                {
                    var exists = await context.PlaybackBookmarks
                        .OfType<SeriesPlaybackBookmark>()
                        .AnyAsync(b => b.UserId == userId && b.SerieId == series.SerieId, cancellationToken);
                    if (exists)
                        continue;

                    context.PlaybackBookmarks.Add(new SeriesPlaybackBookmark
                    {
                        UserId = userId,
                        SerieId = series.SerieId,
                        LastCompletedEpisodeId = series.LastCompletedEpisodeId,
                        NextEpisodeId = series.NextEpisodeId,
                        ActivityAt = series.ActivityAt,
                        NextEpisodeAvailableAt = series.NextEpisodeAvailableAt,
                        UpdatedAt = series.UpdatedAt
                    });
                }
            }
        }
    }

    private static void MergeIntoPersonal(UserMediaState personal, SharedProfileMediaState shared)
    {
        var sharedIsNewer = shared.LastInteractedAt.HasValue
            && (personal.LastInteractedAt is null
                || shared.LastInteractedAt.Value > personal.LastInteractedAt.Value);

        if (sharedIsNewer)
        {
            personal.IsCompleted = shared.IsCompleted;
            personal.LastInteractedAt = shared.LastInteractedAt;
        }

        if (shared.PlayCount > personal.PlayCount)
            personal.PlayCount = shared.PlayCount;

        if (shared.SkipCount > personal.SkipCount)
            personal.SkipCount = shared.SkipCount;
    }
}
