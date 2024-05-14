namespace MediaServer.Domain.Entities.Metadatas.Persons;
public class MusicArtist() : BasePersonRole(PersonRoleType.MusicArtist)
{
    public required bool IsGuest { get; set; }
}
