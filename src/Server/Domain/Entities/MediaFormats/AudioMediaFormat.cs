namespace K7.Server.Domain.Entities.MediaFormats;

public class AudioMediaFormat() : BaseMediaFormat(MediaFormatType.Audio)
{
    public required string Codec { get; set; }
}
