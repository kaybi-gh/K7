namespace K7.Shared.Dtos;

public sealed record MusicMoodPresetDto
{
    public required string MoodKey { get; init; }
    public int CentroidIndex { get; init; }
    public string? TopTags { get; init; }
}
