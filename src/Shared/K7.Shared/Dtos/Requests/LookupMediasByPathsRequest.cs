namespace K7.Shared.Dtos.Requests;

public sealed record LookupMediasByPathsRequest
{
    public required IReadOnlyList<string> Paths { get; init; }
}
