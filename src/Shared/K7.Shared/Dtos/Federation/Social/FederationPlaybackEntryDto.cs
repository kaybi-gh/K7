namespace K7.Shared.Dtos.Federation.Social;

public sealed record FederationPlaybackEntryDto
{
    public required Guid FileId { get; init; }
    public required string UserDisplayName { get; init; }
    public required double Position { get; init; }
    public required DateTimeOffset EndedAt { get; init; }
}
