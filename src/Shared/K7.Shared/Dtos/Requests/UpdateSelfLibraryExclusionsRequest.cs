namespace K7.Shared.Dtos.Requests;

public sealed record UpdateSelfLibraryExclusionsRequest
{
    public required IReadOnlyList<Guid> ExcludedLibraryIds { get; init; }
}
