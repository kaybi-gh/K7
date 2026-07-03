using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Federation.Social;

public sealed record FederatedReviewDto
{
    public required Guid Id { get; init; }
    public required FederatedUserRef Author { get; init; }
    public required FederatedMediaRef Media { get; init; }
    public required string Text { get; init; }
    public string? Emoji { get; init; }
    public required double Rating { get; init; }
    public required DateTimeOffset Created { get; init; }
}
