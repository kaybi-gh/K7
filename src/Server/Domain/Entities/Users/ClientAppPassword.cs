namespace K7.Server.Domain.Entities.Users;

public class ClientAppPassword : BaseAuditableEntity
{
    public required string Name { get; set; }
    public required string PasswordHash { get; set; }
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
