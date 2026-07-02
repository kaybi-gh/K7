using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Services;

public sealed record SyncPlayPlaybackContext(
    string CoWatchingWithSnapshot,
    IReadOnlyList<Guid> CoViewerUserIds);

public interface ISyncPlayPlaybackContextResolver
{
    Task<SyncPlayPlaybackContext?> ResolveAsync(
        Guid syncPlayGroupId,
        Guid currentUserId,
        string? currentIdentityUserId,
        CancellationToken cancellationToken = default);
}

public class SyncPlayPlaybackContextResolver(
    ISyncPlayCoordinator syncPlayCoordinator,
    IApplicationDbContext context) : ISyncPlayPlaybackContextResolver
{
    public async Task<SyncPlayPlaybackContext?> ResolveAsync(
        Guid syncPlayGroupId,
        Guid currentUserId,
        string? currentIdentityUserId,
        CancellationToken cancellationToken = default)
    {
        var group = syncPlayCoordinator.GetGroup(syncPlayGroupId);
        if (group is null)
            return null;

        if (string.IsNullOrEmpty(currentIdentityUserId))
        {
            currentIdentityUserId = await context.Users
                .AsNoTracking()
                .Where(u => u.Id == currentUserId)
                .Select(u => u.IdentityUserId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (string.IsNullOrEmpty(currentIdentityUserId))
            return null;

        var isMember = group.Members.Values.Any(m =>
            !m.IsGuest
            && string.Equals(m.IdentityUserId, currentIdentityUserId, StringComparison.Ordinal));

        if (!isMember)
            return null;

        var otherLabels = new List<string>();
        var seenParticipants = new HashSet<string>(StringComparer.Ordinal);

        foreach (var member in group.Members.Values)
        {
            if (!member.IsGuest
                && string.Equals(member.IdentityUserId, currentIdentityUserId, StringComparison.Ordinal))
            {
                continue;
            }

            var participantKey = member.IsGuest
                ? $"guest:{member.DeviceId}"
                : member.IdentityUserId ?? member.DeviceId.ToString();

            if (!seenParticipants.Add(participantKey))
                continue;

            otherLabels.Add(member.DisplayName);
        }

        if (otherLabels.Count == 0)
            return null;

        var otherIdentityUserIds = group.Members.Values
            .Where(m => !m.IsGuest
                && m.IdentityUserId is not null
                && !string.Equals(m.IdentityUserId, currentIdentityUserId, StringComparison.Ordinal))
            .Select(m => m.IdentityUserId!)
            .Distinct()
            .ToList();

        var coViewerUserIds = otherIdentityUserIds.Count == 0
            ? []
            : await context.Users
                .AsNoTracking()
                .Where(u => u.IdentityUserId != null && otherIdentityUserIds.Contains(u.IdentityUserId))
                .Select(u => u.Id)
                .ToListAsync(cancellationToken);

        return new SyncPlayPlaybackContext(string.Join(", ", otherLabels), coViewerUserIds);
    }
}
