namespace K7.Shared.Dtos.Entities.PersonRoles;

public sealed record MusicArtistRoleDto : PersonRoleDto
{
    public bool IsGuest { get; init; }
}
