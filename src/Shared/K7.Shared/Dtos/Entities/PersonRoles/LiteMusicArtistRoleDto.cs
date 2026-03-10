namespace K7.Shared.Dtos.Entities.PersonRoles;

public sealed record LiteMusicArtistRoleDto : LitePersonRoleDto
{
    public bool IsGuest { get; init; }
}
