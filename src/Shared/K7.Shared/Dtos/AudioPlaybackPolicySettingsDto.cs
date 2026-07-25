namespace K7.Shared.Dtos;

public sealed record AudioPlaybackPolicySettingsDto
{
    public int CompletedThresholdPercent { get; set; } = 50;
    public int CompletedMinDurationSeconds { get; set; } = 240;
}
