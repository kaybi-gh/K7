namespace K7.Import.Models;

public sealed record SourceUser
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    /// <summary>Optional qualifier shown in CLI feedback (e.g. Tracearr server name).</summary>
    public string? Detail { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(Detail) ? Name : $"{Name} ({Detail})";
}
