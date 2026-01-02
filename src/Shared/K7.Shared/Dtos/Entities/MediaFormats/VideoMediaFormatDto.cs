namespace K7.Shared.Dtos.Entities.Medias;

public sealed record VideoMediaFormatDto : MediaFormatDto
{
    public string? AudioCodec { get; init; }
    public required string VideoCodec { get; init; }
}
