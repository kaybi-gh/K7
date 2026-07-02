using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Users;

public class MediaPlaybackSession : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public Guid MediaId { get; set; }
    public BaseMedia Media { get; set; } = null!;

    public Guid SessionId { get; set; }
    public Guid ReferenceId { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? LastUpdateAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
    public double PositionSeconds { get; set; }
    public double DurationSeconds { get; set; }
    public double WatchedDurationSeconds { get; set; }
    public PlaybackState State { get; set; }

    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }

    public Guid? ViewingGroupId { get; set; }
    public ViewingGroup? ViewingGroup { get; set; }
    public string? ViewingGroupNameSnapshot { get; set; }

    public PlaybackSessionDetails? Details { get; set; }
}
