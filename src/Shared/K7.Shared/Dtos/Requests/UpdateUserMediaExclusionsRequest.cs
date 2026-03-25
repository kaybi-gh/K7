namespace K7.Shared.Dtos.Requests;

public sealed record UpdateUserMediaExclusionsRequest
{
    public required List<Guid> ExcludedMediaIds { get; init; }
}
