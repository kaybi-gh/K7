using K7.Server.Domain.Common;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Notifications;

public class UserScrobblerAccount : BaseAuditableEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public ScrobblerProvider Provider { get; set; }
    public bool IsEnabled { get; set; } = true;
    public required string ConfigJson { get; set; }
    public List<MediaType> MediaTypes { get; set; } = [];
    public bool IncludeNowPlaying { get; set; } = true;
    public string? DisplayName { get; set; }
}
