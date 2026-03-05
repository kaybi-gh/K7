namespace K7.Shared.Dtos.Entities.Metadatas;

public sealed record MetadataSearchResult
{
    public string Provider { get; init; } = string.Empty;
    public string ExternalId { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public int? Year { get; init; }
    public string? PosterUrl { get; init; }
    public string? Overview { get; init; }
}
