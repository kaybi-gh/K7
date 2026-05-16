namespace K7.Shared.Dtos.Entities.PersonRoles;

public sealed record LiteMusicArtistRoleDto : LitePersonRoleDto
{
    public string? Role { get; init; }
    public bool IsActive { get; init; }
}
