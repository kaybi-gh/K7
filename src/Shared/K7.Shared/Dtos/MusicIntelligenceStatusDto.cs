namespace K7.Shared.Dtos;

public sealed record MusicIntelligenceStatusDto
{
    public bool IsEnabled { get; init; }
    public bool IsAvailable { get; init; }
    public string Provider { get; init; } = "audiomuse";
}
