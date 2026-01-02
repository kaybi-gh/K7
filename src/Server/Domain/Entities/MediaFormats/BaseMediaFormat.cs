namespace K7.Server.Domain.Entities.MediaFormats;

public abstract class BaseMediaFormat(MediaFormatType type)
{
    public MediaFormatType Type { get; private set; } = type;
    public required string Id { get; set; }
    public required string Container { get; set; }
}
