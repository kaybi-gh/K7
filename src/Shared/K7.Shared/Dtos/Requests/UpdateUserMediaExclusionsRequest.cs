namespace K7.Shared.Dtos.Requests;

public sealed record UpdateUserMediaExclusionsRequest
{
    public required IReadOnlyList<Guid> ExcludedMediaIds { get; init; }
}
