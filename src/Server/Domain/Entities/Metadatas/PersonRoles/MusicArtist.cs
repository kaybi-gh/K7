namespace K7.Server.Domain.Entities.Metadatas.PersonRoles;
public class MusicArtist() : BasePersonRole(PersonRoleType.MusicArtist)
{
    public required bool IsGuest { get; set; }
}
