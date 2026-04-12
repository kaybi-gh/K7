namespace K7.Import.Models;

public sealed record SourceUser
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}
