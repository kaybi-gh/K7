using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;

namespace K7.Server.Application.Features.MediaShares.Services;

/// <summary>
/// Turns the visitor session tokens of a request into the shares they still open.
/// </summary>
public interface IShareSessionValidator
{
    Task<IReadOnlyList<ShareVisitorGrant>> ValidateAsync(
        IReadOnlyCollection<string> sessionTokens,
        CancellationToken cancellationToken = default);
}

public class ShareSessionValidator(
    IApplicationDbContext context,
    IMediaShareManager shareManager) : IShareSessionValidator
{
    public async Task<IReadOnlyList<ShareVisitorGrant>> ValidateAsync(
        IReadOnlyCollection<string> sessionTokens,
        CancellationToken cancellationToken = default)
    {
        var hashes = sessionTokens
            .Where(ShareTokenHelper.LooksValid)
            .Select(ShareTokenHelper.Hash)
            .Distinct()
            .ToList();

        if (hashes.Count == 0 || !await shareManager.IsEnabledAsync(cancellationToken))
            return [];

        var sessions = await context.MediaShareSessions
            .AsNoTracking()
            .Include(s => s.Share)
            .Where(s => hashes.Contains(s.TokenHash))
            .ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var grants = new List<ShareVisitorGrant>();
        foreach (var session in sessions)
        {
            if (!session.IsUsable(now) || !session.Share.AllowsExistingSession(session.CreatedAt, now))
                continue;

            // A share never outlives the right to share of the user who created it.
            if (!await shareManager.CanUserShareAsync(session.Share.CreatedByUserId, cancellationToken))
                continue;

            if (grants.All(g => g.ShareId != session.ShareId))
                grants.Add(new ShareVisitorGrant(session.ShareId, session.Id));
        }

        if (grants.Count > 0)
        {
            var sessionIds = grants.Select(g => g.SessionId).ToList();
            await context.MediaShareSessions
                .Where(s => sessionIds.Contains(s.Id))
                .ExecuteUpdateAsync(set => set.SetProperty(s => s.LastSeenAt, now), cancellationToken);
        }

        return grants;
    }
}
