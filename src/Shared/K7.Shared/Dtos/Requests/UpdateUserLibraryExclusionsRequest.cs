namespace K7.Shared.Dtos.Requests;

public sealed record UpdateUserLibraryExclusionsRequest
{
    public required IReadOnlyList<Guid> ExcludedLibraryIds { get; init; }
}
