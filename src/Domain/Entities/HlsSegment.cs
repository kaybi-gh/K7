namespace MediaServer.Domain.Entities;

public class HlsSegment
{
    public Guid VideoFileMetadataId { get; set; }
    public int Number {  get; set; }
    public long StartTimestamp { get; set; }
    public long Duration { get; set; }
}
