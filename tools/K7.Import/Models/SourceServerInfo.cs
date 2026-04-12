namespace K7.Import.Models;

public sealed record SourceServerInfo
{
    public required string Name { get; init; }
    public string? Version { get; init; }
}
