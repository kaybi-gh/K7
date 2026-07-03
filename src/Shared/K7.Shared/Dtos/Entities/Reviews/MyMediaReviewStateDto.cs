namespace K7.Shared.Dtos.Entities.Reviews;

public sealed record MyMediaReviewStateDto
{
    public int? Rating { get; init; }
    public bool HasReview { get; init; }
    public string? Text { get; init; }
    public string? Emoji { get; init; }
}
