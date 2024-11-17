namespace K7.Server.Domain.Entities.Metadatas.Files.Tracks;

public class VideoFileTrack() : BaseFileTrack(FileTrackType.Video)
{
    public Guid? VideoFileMetadataId { get; set; }
    public required int Width { get; set; }
    public required int Height { get; set; }
    public required string CodecName { get; set; }
    public required string Profile { get; set; }
    public required int Level { get; set; }
    public string? PixelFormat { get; set; }
    public int? BitDepth { get; set; }
}
