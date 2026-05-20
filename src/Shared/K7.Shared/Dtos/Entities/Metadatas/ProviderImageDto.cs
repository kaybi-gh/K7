using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities.Metadatas;

public sealed record ProviderImageDto
{
    public required string Url { get; init; }
    public required string ThumbnailUrl { get; init; }
    public required MetadataPictureType Type { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public double VoteAverage { get; init; }
    public string? Language { get; init; }
}
