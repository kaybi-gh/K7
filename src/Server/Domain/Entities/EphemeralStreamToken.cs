using K7.Server.Domain.Common;
using K7.Server.Domain.Entities.Users;

namespace K7.Server.Domain.Entities;

public class EphemeralStreamToken : BaseEntity
{
    public required string Token { get; set; }

    public Guid StreamSessionId { get; set; }
    public StreamSession StreamSession { get; set; } = null!;

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public bool IsUsable(DateTimeOffset utcNow) => !IsRevoked && ExpiresAt >= utcNow;
}
