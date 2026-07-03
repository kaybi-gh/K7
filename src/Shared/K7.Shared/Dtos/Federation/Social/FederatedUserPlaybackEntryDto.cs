namespace K7.Shared.Dtos.Federation.Social;

public sealed record FederatedUserPlaybackEntryDto
{
    public required Guid OriginUserId { get; init; }
    public required Guid MediaId { get; init; }
    public required string MediaTitle { get; init; }
    public required DateTimeOffset EndedAt { get; init; }
}
