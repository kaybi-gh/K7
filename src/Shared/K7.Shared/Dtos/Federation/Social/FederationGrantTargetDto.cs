namespace K7.Shared.Dtos.Federation.Social;

public enum FederationGrantTargetKind
{
    LocalUser = 0,
    PeerServer = 1,
    FederatedUser = 2
}

public sealed record FederationGrantTargetDto
{
    public required FederationGrantTargetKind Kind { get; init; }
    public required string Label { get; init; }
    public Guid? TargetUserId { get; init; }
    public Guid? TargetPeerServerId { get; init; }
    public Guid? TargetOriginUserId { get; init; }
}
