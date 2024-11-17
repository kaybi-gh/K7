namespace K7.Server.Domain.Entities;

public class HlsSegment
{
    public Guid FileMetadataId { get; set; }
    public Guid IndexedFileId { get; set; }
    public int Number {  get; set; }
    public long StartTimestamp { get; set; }
    public long Duration { get; set; }
}
