namespace K7.Shared.Dtos.Requests;

public sealed record LookupIndexedPathsByFileNamesRequest
{
    public required IReadOnlyList<string> FileNames { get; init; }
}
