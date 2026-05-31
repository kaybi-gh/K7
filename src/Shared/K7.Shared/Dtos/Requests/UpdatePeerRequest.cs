namespace K7.Shared.Dtos.Requests;

public sealed record UpdatePeerRequest
{
    public string? BaseUrl { get; init; }
    public IReadOnlyList<Guid>? SharedLibraryIds { get; init; }
    public IReadOnlyList<Guid>? EnabledInboundAgreementIds { get; init; }
    public int? MaxConcurrentStreams { get; init; }
    public bool? AutoAddNewLibraries { get; init; }
}
