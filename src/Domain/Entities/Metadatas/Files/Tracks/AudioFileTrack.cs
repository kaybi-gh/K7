namespace MediaServer.Domain.Entities.Metadatas.Files.Tracks;

public class AudioFileTrack() : BaseFileTrack(FileTrackType.Audio)
{
    public Guid? AudioFileMetadataId { get; set; }
    public Guid? VideoFileMetadataId { get; set; }
    public string? Name { get; set; }
    public string? Language { get; set; }
}
