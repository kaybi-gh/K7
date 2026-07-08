namespace K7.Shared.Dtos;

public sealed record AudioPlaybackPolicySettingsDto
{
    public int MinResumePercent { get; set; } = 5;
    public int MinResumeDurationSeconds { get; set; } = 30;
    public int CompletedThresholdPercent { get; set; } = 50;
    public int CompletedMinDurationSeconds { get; set; } = 240;
}
