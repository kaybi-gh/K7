namespace MediaServer.Domain.Entities.Metadatas.Persons;
public class MusicArtist() : BasePersonRole(PersonJob.MusicArtist)
{
    public required bool IsGuest { get; set; }
}
