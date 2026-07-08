namespace K7.Shared.Dtos;

public sealed record VideoPlaybackPolicySettingsDto
{
    public int MinResumePercent { get; set; } = 5;
    public int MinResumeDurationSeconds { get; set; } = 300;
    public int CompletedThresholdPercent { get; set; } = 90;
    public int ContinueWatchingMaxAgeDays { get; set; } = 14;
}
