namespace K7.Shared.Dtos;

public sealed record SetMediaWatchStateResultDto
{
    public required IReadOnlyList<Guid> AffectedMediaIds { get; init; }
}
