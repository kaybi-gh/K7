using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Shares;

/// <summary>
/// Public, temporary link to one media and everything below it.
/// The scope is the root media plus its descendants, never its parents or siblings.
/// </summary>
public class MediaShare : BaseAuditableEntity
{
    /// <summary>
    /// How long a visitor that was already watching keeps access once the share is consumed.
    /// Lets the end credits and a download in progress finish.
    /// </summary>
    public static readonly TimeSpan ConsumedGracePeriod = TimeSpan.FromMinutes(30);

    public Guid RootMediaId { get; set; }
    public BaseMedia RootMedia { get; set; } = null!;

    public required string TokenHash { get; set; }
    public string? Label { get; set; }
    public string? PasswordHash { get; set; }

    /// <summary>Wrong passwords since the last success or lock.</summary>
    public int FailedPasswordAttempts { get; set; }

    /// <summary>No password is accepted before this time (too many wrong attempts).</summary>
    public DateTimeOffset? PasswordLockedUntil { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }
    public int? MaxAccessCount { get; set; }
    public int AccessCount { get; set; }

    public bool AllowPlayback { get; set; } = true;
    public bool AllowDownload { get; set; }

    /// <summary>
    /// Close the share once its media was played to the end or downloaded.
    /// Only meaningful for a single playable item (movie, episode, track).
    /// </summary>
    public bool CloseWhenConsumed { get; set; }
    public DateTimeOffset? ConsumedAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }
    public DateTimeOffset? LastAccessedAt { get; set; }

    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;

    public IList<MediaShareSession> Sessions { get; set; } = [];

    public bool HasPassword => !string.IsNullOrEmpty(PasswordHash);

    public MediaShareStatus GetStatus(DateTimeOffset utcNow)
    {
        if (RevokedAt is not null)
            return MediaShareStatus.Revoked;

        if (ExpiresAt is { } expiresAt && expiresAt <= utcNow)
            return MediaShareStatus.Expired;

        if (ConsumedAt is not null)
            return MediaShareStatus.Consumed;

        if (MaxAccessCount is { } max && AccessCount >= max)
            return MediaShareStatus.Exhausted;

        return MediaShareStatus.Active;
    }

    /// <summary>
    /// True when a new visitor may open the share.
    /// </summary>
    public bool AcceptsNewSessions(DateTimeOffset utcNow) =>
        GetStatus(utcNow) == MediaShareStatus.Active;

    /// <summary>
    /// True when a visitor that already opened the share may keep using it.
    /// Reaching the access limit does not cut visitors that are already in.
    /// </summary>
    public bool AllowsExistingSession(DateTimeOffset sessionCreatedAt, DateTimeOffset utcNow)
    {
        if (RevokedAt is not null)
            return false;

        if (ExpiresAt is { } expiresAt && expiresAt <= utcNow)
            return false;

        if (ConsumedAt is { } consumedAt)
            return sessionCreatedAt <= consumedAt && utcNow <= consumedAt + ConsumedGracePeriod;

        return true;
    }
}
