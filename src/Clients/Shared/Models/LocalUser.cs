namespace K7.Clients.Shared.Models;

public sealed class LocalUser
{
    public required string IdentityUserId { get; init; }
    public Guid? UserId { get; set; }
    public required string UserName { get; set; }
    public string? DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Email { get; set; }
    public string? PinHash { get; set; }
    public required string RefreshToken { get; set; }
}
