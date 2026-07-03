namespace K7.Shared.Dtos.Requests;

using K7.Shared.Dtos.Entities;

public sealed record UpdatePeerRequest
{
    public string? BaseUrl { get; init; }
    public IReadOnlyList<Guid>? SharedLibraryIds { get; init; }
    public IReadOnlyList<Guid>? EnabledInboundAgreementIds { get; init; }
    public int? MaxConcurrentStreams { get; init; }
    public bool? AutoAddNewLibraries { get; init; }
    public IReadOnlyList<PeerSocialAgreementDto>? SocialAgreements { get; init; }
    public IReadOnlyList<Guid>? SharePlaybackHistoryLibraryIds { get; init; }
}
