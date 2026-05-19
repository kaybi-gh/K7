namespace K7.Shared.Dtos.Entities.Persons;

public record PersonKnownForItemDto
{
    public required string ExternalId { get; init; }
    public required string Title { get; init; }
    public int? Year { get; init; }
    public required string MediaType { get; init; }
    public string? PosterUrl { get; init; }
}
