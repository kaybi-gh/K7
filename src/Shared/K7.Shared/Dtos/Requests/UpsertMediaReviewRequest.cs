namespace K7.Shared.Dtos.Requests;

public sealed record UpsertMediaReviewRequest
{
    public required string Text { get; init; }
    public string? Emoji { get; init; }
    public required int Rating { get; init; }
}
