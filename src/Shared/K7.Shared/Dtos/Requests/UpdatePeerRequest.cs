namespace K7.Shared.Dtos.Requests;

public sealed record UpdatePeerRequest
{
    public IReadOnlyList<Guid> SharedLibraryIds { get; init; } = [];
    public int? MaxConcurrentStreams { get; init; }
}
