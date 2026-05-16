namespace K7.Shared.Dtos.Entities.PersonRoles;

public sealed record MusicArtistRoleDto : PersonRoleDto
{
    public string? Role { get; init; }
    public bool IsActive { get; init; }
}
