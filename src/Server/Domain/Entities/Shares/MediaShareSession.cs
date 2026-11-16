using K7.Server.Domain.Entities.Devices;

namespace K7.Server.Domain.Entities.Shares;

/// <summary>
/// One visitor that opened a share. Counts as one access.
/// </summary>
public class MediaShareSession : BaseEntity
{
    public static readonly TimeSpan MaxLifetime = TimeSpan.FromHours(24);

    public Guid ShareId { get; set; }
    public MediaShare Share { get; set; } = null!;

    public required string TokenHash { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset LastSeenAt { get; set; }

    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    /// <summary>
    /// Technical device holding the visitor browser capabilities for stream negotiation.
    /// </summary>
    public Guid? DeviceId { get; set; }
    public Device? Device { get; set; }

    public bool IsUsable(DateTimeOffset utcNow) => ExpiresAt > utcNow;
}
