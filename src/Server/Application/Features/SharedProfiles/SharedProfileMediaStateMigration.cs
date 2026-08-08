using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.SharedProfiles;

/// <summary>
/// Copies shared-profile playback progress into each member's personal <see cref="UserMediaState"/>
/// before a shared profile is deleted (cascade would otherwise drop the shared rows).
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

        if (sharedStates.Count == 0)
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
                    LastPlaybackPosition = shared.LastPlaybackPosition,
                    ProgressPercentage = shared.ProgressPercentage,
                    IsCompleted = shared.IsCompleted,
                    PlayCount = shared.PlayCount,
                    SkipCount = shared.SkipCount,
                    LastInteractedAt = shared.LastInteractedAt,
                    LastKnownDurationSeconds = shared.LastKnownDurationSeconds,
                    ExcludedFromContinueWatching = shared.ExcludedFromContinueWatching
                };
                context.UserMediaStates.Add(created);
                existingByUserAndMedia[(userId, shared.MediaId)] = created;
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
            personal.LastPlaybackPosition = shared.LastPlaybackPosition;
            personal.ProgressPercentage = shared.ProgressPercentage;
            personal.IsCompleted = shared.IsCompleted;
            personal.LastInteractedAt = shared.LastInteractedAt;
            personal.LastKnownDurationSeconds = shared.LastKnownDurationSeconds;
            personal.ExcludedFromContinueWatching = shared.ExcludedFromContinueWatching;
        }

        if (shared.PlayCount > personal.PlayCount)
            personal.PlayCount = shared.PlayCount;

        if (shared.SkipCount > personal.SkipCount)
            personal.SkipCount = shared.SkipCount;
    }
}
