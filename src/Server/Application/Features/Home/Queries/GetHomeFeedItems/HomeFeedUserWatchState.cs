using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Mappings;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Home.Queries.GetHomeFeedItems;

/// <summary>
/// Attaches personal or shared-profile watch state onto media graphs used by non-CW home rows.
/// </summary>
internal static class HomeFeedUserWatchState
{
    public static async Task ApplyAsync(
        IApplicationDbContext context,
        IReadOnlyList<BaseMedia> items,
        Guid userId,
        Guid? sharedProfileId,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
            return;

        var mediaIds = items.Select(i => i.Id).ToList();

        if (sharedProfileId is { } profileId)
        {
            var sharedStates = await context.SharedProfileMediaStates
                .AsNoTracking()
                .Where(s => s.SharedProfileId == profileId && mediaIds.Contains(s.MediaId))
                .ToDictionaryAsync(s => s.MediaId, cancellationToken);

            foreach (var item in items)
            {
                item.UserMediaStates.Clear();
                if (sharedStates.TryGetValue(item.Id, out var shared))
                    item.UserMediaStates.Add(shared.ToUserMediaState(userId));
            }

            return;
        }

        var userStates = await context.UserMediaStates
            .AsNoTracking()
            .Where(s => s.UserId == userId && mediaIds.Contains(s.MediaId))
            .ToDictionaryAsync(s => s.MediaId, cancellationToken);

        foreach (var item in items)
        {
            item.UserMediaStates.Clear();
            if (userStates.TryGetValue(item.Id, out var state))
                item.UserMediaStates.Add(state);
        }
    }
}
