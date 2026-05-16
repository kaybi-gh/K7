namespace K7.Shared.Dtos;

public sealed record ServerFeatureFlagsDto
{
    public bool IntroDetectionEnabled { get; init; } = true;
    public bool TranscodingEnabled { get; init; } = true;
    public bool TransmuxingEnabled { get; init; } = true;
    public bool SeekbarThumbnailGenerationEnabled { get; init; } = true;
    public bool MusicAudioAnalysisEnabled { get; init; } = true;
}
