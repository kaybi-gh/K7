namespace MediaServer.Domain.Entities.Metadatas.Files;

public class VideoInfo
{
    public VideoResolutionIdentifier Quality { get; set; }
    public int ResolutionWidth { get; set; }
    public int ResolutionHeight { get; set; }
    public long Bitrate { get; set; }
    public string? Codec { get; set; }
}
