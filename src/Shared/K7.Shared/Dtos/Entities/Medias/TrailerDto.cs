namespace K7.Shared.Dtos.Entities.Medias;

public sealed record TrailerDto
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Site { get; init; }
    public required string Type { get; init; }
    public string? Language { get; init; }
}
