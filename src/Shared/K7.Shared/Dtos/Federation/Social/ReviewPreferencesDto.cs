namespace K7.Shared.Dtos.Federation.Social;

public sealed record ReviewPreferencesDto
{
    public bool BlurReviewsForUnwatchedMedia { get; set; } = true;
}
