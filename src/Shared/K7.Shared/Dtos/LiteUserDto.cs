using K7.Server.Domain.Entities.Users;

namespace K7.Shared.Dtos;

public sealed record LiteUserDto
{
    public Guid Id { get; init; }
    public string? IdentityUserId { get; init; }

    public static LiteUserDto FromDomain(User domain) => new()
    {
        Id = domain.Id,
        IdentityUserId = domain.IdentityUserId
    };
}
