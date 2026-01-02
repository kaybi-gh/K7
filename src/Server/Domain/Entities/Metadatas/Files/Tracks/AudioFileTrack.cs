namespace K7.Server.Domain.Entities.Metadatas.Files.Tracks;

public class AudioFileTrack() : BaseFileTrack(FileTrackType.Audio)
{
    public Guid? AudioFileMetadataId { get; set; }
    public Guid? VideoFileMetadataId { get; set; }
    public string? Name { get; set; }
    public string? Language { get; set; }
    public required string Codec { get; set; }
    public required int Channels {  get; set; }
    public string? ChannelLayout { get; set; }
    public int? SampleRateHz { get; set; }
    public string? Profile { get; set; }
}
