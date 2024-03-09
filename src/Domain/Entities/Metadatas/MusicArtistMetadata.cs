namespace MediaServer.Domain.Entities.Metadatas;
public class MusicArtistMetadata() : BaseMetadata(MediaType.MusicArtist)
{
    public string? Name { get; set; }
}
