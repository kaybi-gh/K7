namespace K7.Server.Domain.Entities.MediaFormats;

public class VideoMediaFormat() : BaseMediaFormat(MediaFormatType.Video)
{
    public string? AudioCodec { get; set; }
    public required string VideoCodec { get; set; }
}
