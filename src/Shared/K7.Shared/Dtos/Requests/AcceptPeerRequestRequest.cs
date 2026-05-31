namespace K7.Shared.Dtos.Requests;

public sealed record AcceptPeerRequestRequest
{
    public IReadOnlyList<Guid> SharedLibraryIds { get; init; } = [];
    public bool AutoShareNewLibraries { get; init; }
    public int? MaxConcurrentStreams { get; init; }
}
