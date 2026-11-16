using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.MediaShares.Services;
using K7.Server.Domain.Entities.Shares;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Shares;

namespace K7.Server.Application.Features.MediaShares.Commands.OpenMediaShare;

/// <summary>
/// A visitor opens a share link. Anonymous on purpose.
/// Opening creates a visitor session and counts as one access. A visitor that comes back
/// with a session that is still valid reuses it and is not counted again.
/// </summary>
public record OpenMediaShareCommand : IRequest<OpenMediaShareResult>
{
    public required string Token { get; init; }
    public string? Password { get; init; }

    /// <summary>Session tokens the visitor already holds (from its cookie).</summary>
    public IReadOnlyCollection<string> ExistingSessionTokens { get; init; } = [];
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}

/// <param name="SessionToken">Token to hand to the visitor. Null when no session is open.</param>
/// <param name="IsNewSession">False when an existing session was reused.</param>
public sealed record OpenMediaShareResult(OpenShareResultDto Dto, string? SessionToken, bool IsNewSession);

public class OpenMediaShareCommandHandler(
    IApplicationDbContext context,
    IMediaShareManager shareManager)
    : IRequestHandler<OpenMediaShareCommand, OpenMediaShareResult>
{
    public async Task<OpenMediaShareResult> Handle(OpenMediaShareCommand request, CancellationToken cancellationToken)
    {
        if (!ShareTokenHelper.LooksValid(request.Token) || !await shareManager.IsEnabledAsync(cancellationToken))
            throw new NotFoundException("share", nameof(MediaShare));

        var tokenHash = ShareTokenHelper.Hash(request.Token);
        var share = await context.MediaShares
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TokenHash == tokenHash, cancellationToken);

        if (share is null)
            throw new NotFoundException("share", nameof(MediaShare));

        var now = DateTimeOffset.UtcNow;
        var canShare = await shareManager.CanUserShareAsync(share.CreatedByUserId, cancellationToken);

        var rootType = await context.Medias
            .AsNoTracking()
            .Where(m => m.Id == share.RootMediaId)
            .Select(m => m.Type)
            .FirstAsync(cancellationToken);

        // A returning visitor keeps its session: no password prompt, no new access.
        if (canShare)
        {
            var reused = await FindReusableSessionAsync(share, request.ExistingSessionTokens, now, cancellationToken);
            if (reused is not null)
                return new OpenMediaShareResult(Opened(share, rootType, reused.Value.ExpiresAt), reused.Value.Token, IsNewSession: false);
        }

        var status = MediaShareManager.ResolveStatus(share, canShare, now);
        if (status != MediaShareStatus.Active)
            return Refused(OpenShareOutcome.Unavailable, status);

        if (share.HasPassword)
        {
            // The link holder is the only one who can try passwords: the per-IP rate limit is
            // not enough against that, so the share itself stops listening for a while.
            if (share.PasswordLockedUntil is { } lockedUntil && lockedUntil > now)
                return Refused(OpenShareOutcome.PasswordLocked, status);

            if (string.IsNullOrEmpty(request.Password))
                return Refused(OpenShareOutcome.PasswordRequired, status);

            if (!PinHashHelper.Verify(share.PasswordHash!, request.Password))
            {
                var locked = await RecordFailedPasswordAsync(share.Id, now, cancellationToken);
                return Refused(locked ? OpenShareOutcome.PasswordLocked : OpenShareOutcome.InvalidPassword, status);
            }

            if (share.FailedPasswordAttempts > 0)
            {
                await context.MediaShares
                    .Where(s => s.Id == share.Id)
                    .ExecuteUpdateAsync(set => set.SetProperty(s => s.FailedPasswordAttempts, 0), cancellationToken);
            }
        }

        // Atomic: two visitors racing for the last access cannot both get in.
        var counted = await context.MediaShares
            .Where(s => s.Id == share.Id
                && s.RevokedAt == null
                && (s.MaxAccessCount == null || s.AccessCount < s.MaxAccessCount))
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(s => s.AccessCount, s => s.AccessCount + 1)
                    .SetProperty(s => s.LastAccessedAt, now),
                cancellationToken);

        if (counted == 0)
            return Refused(OpenShareOutcome.Unavailable, MediaShareStatus.Exhausted);

        var sessionToken = ShareTokenHelper.Generate();
        var expiresAt = now + MediaShareSession.MaxLifetime;
        if (share.ExpiresAt is { } shareExpiresAt && shareExpiresAt < expiresAt)
            expiresAt = shareExpiresAt;

        context.MediaShareSessions.Add(new MediaShareSession
        {
            Id = Guid.NewGuid(),
            ShareId = share.Id,
            TokenHash = ShareTokenHelper.Hash(sessionToken),
            CreatedAt = now,
            ExpiresAt = expiresAt,
            LastSeenAt = now,
            IpAddress = Truncate(request.IpAddress, 64),
            UserAgent = Truncate(request.UserAgent, 512)
        });
        await context.SaveChangesAsync(cancellationToken);

        return new OpenMediaShareResult(Opened(share, rootType, expiresAt), sessionToken, IsNewSession: true);
    }

    /// <summary>
    /// Counts a wrong password. Returns true when this attempt locks the share.
    /// </summary>
    private async Task<bool> RecordFailedPasswordAsync(Guid shareId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await context.MediaShares
            .Where(s => s.Id == shareId)
            .ExecuteUpdateAsync(
                set => set.SetProperty(s => s.FailedPasswordAttempts, s => s.FailedPasswordAttempts + 1),
                cancellationToken);

        var lockUntil = now + MediaShareRules.PasswordLockDuration;
        var locked = await context.MediaShares
            .Where(s => s.Id == shareId && s.FailedPasswordAttempts >= MediaShareRules.MaxPasswordAttempts)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(s => s.FailedPasswordAttempts, 0)
                    .SetProperty(s => s.PasswordLockedUntil, lockUntil),
                cancellationToken);

        return locked > 0;
    }

    private async Task<(string Token, DateTimeOffset ExpiresAt)?> FindReusableSessionAsync(
        MediaShare share,
        IReadOnlyCollection<string> existingTokens,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var candidates = existingTokens
            .Where(ShareTokenHelper.LooksValid)
            .Distinct()
            .ToDictionary(ShareTokenHelper.Hash, token => token);

        if (candidates.Count == 0)
            return null;

        var hashes = candidates.Keys.ToList();
        var sessions = await context.MediaShareSessions
            .AsNoTracking()
            .Where(s => s.ShareId == share.Id && hashes.Contains(s.TokenHash))
            .Select(s => new { s.TokenHash, s.CreatedAt, s.ExpiresAt })
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
        {
            if (session.ExpiresAt > now && share.AllowsExistingSession(session.CreatedAt, now))
                return (candidates[session.TokenHash], session.ExpiresAt);
        }

        return null;
    }

    private static OpenShareResultDto Opened(MediaShare share, MediaType rootType, DateTimeOffset sessionExpiresAt) => new()
    {
        Outcome = OpenShareOutcome.Opened,
        Status = MediaShareStatus.Active,
        ShareId = share.Id,
        RootMediaId = share.RootMediaId,
        RootMediaType = rootType,
        AllowPlayback = share.AllowPlayback,
        AllowDownload = share.AllowDownload,
        SessionExpiresAt = sessionExpiresAt
    };

    private static OpenMediaShareResult Refused(OpenShareOutcome outcome, MediaShareStatus status) =>
        new(new OpenShareResultDto { Outcome = outcome, Status = status }, SessionToken: null, IsNewSession: false);

    private static string? Truncate(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength ? value : value[..maxLength];
}
