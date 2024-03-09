namespace MediaServer.Domain.Entities.Metadatas;
public class MusicArtistMetadata : BaseMetadata
{
    public MusicArtistMetadata() : base(MediaType.MusicArtist) { }

    public string? Name { get; set; }
}
