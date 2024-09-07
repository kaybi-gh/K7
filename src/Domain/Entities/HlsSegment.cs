namespace MediaServer.Domain.Entities;

public class HlsSegment
{
    public int Id {  get; set; }
    public Guid VideoFileMetadataId { get; set; }
    public int SegmentId { get; set; }
    public TimeSpan Duration { get; set; }
    public TimeOnly Keyframe {  get; set; }
}
