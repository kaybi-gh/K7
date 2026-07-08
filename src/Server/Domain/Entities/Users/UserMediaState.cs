using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Domain.Entities.Users;

public class UserMediaState : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid MediaId { get; set; }
    public BaseMedia Media { get; set; } = null!;

    public double LastPlaybackPosition { get; set; }
    public double ProgressPercentage { get; set; }
    public bool IsCompleted { get; set; }
    public int PlayCount { get; set; }
    public DateTime? LastInteractedAt { get; set; }
    public double LastKnownDurationSeconds { get; set; }
    public bool ExcludedFromContinueWatching { get; set; }
}
