using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities.Reviews;

public sealed record MediaReviewDto
{
    public required Guid Id { get; init; }
    public required Guid UserId { get; init; }
    public required Guid MediaId { get; init; }
    public required Guid UserRatingId { get; init; }
    public required string Text { get; init; }
    public string? Emoji { get; init; }
    public required double Rating { get; init; }
    public string? UserDisplayName { get; init; }
    public required DateTimeOffset Created { get; init; }
    public DateTimeOffset? LastModified { get; init; }
}
