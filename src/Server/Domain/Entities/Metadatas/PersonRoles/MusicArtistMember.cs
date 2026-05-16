namespace K7.Server.Domain.Entities.Metadatas.PersonRoles;

public class MusicArtistMember() : BasePersonRole(PersonRoleType.MusicArtist)
{
    public string? Role { get; set; }
    public bool IsActive { get; set; } = true;
}

