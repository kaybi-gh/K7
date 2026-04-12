namespace K7.Import.Models;

public sealed record SourceLibrary
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? MediaType { get; init; }
    public int? ItemCount { get; init; }
}
