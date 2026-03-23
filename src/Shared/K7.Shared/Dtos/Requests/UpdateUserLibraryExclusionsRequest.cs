namespace K7.Shared.Dtos.Requests;

public sealed record UpdateUserLibraryExclusionsRequest
{
    public required List<Guid> ExcludedLibraryIds { get; init; }
}
