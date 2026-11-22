namespace K7.Server.Domain.Entities.Users;

/// <summary>
/// Lets <see cref="GuardianUser"/> open a session for <see cref="User"/> (the managed account)
/// from the device-link page, without signing in as that account. An account is managed when it
/// has at least one guardian.
/// </summary>
public class UserGuardian : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid GuardianUserId { get; set; }
    public User GuardianUser { get; set; } = null!;
}
