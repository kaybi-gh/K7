namespace K7.Shared.Dtos.Entities.Medias;

public sealed record AudioMediaFormatDto : MediaFormatDto
{
    public required string Codec { get; init; }
}
