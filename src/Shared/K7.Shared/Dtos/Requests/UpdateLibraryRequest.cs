namespace K7.Shared.Dtos.Requests;

public sealed record UpdateLibraryRequest
{
    public string? Title { get; init; }
    public string? MetadataProviderName { get; init; }
    public string? MetadataLanguage { get; init; }
    public string? MetadataFallbackLanguage { get; init; }
    public int? MetadataRefreshIntervalDays { get; init; }
    public Guid? LibraryGroupId { get; init; }
    public bool? IntroDetectionEnabled { get; init; }
    public bool? ThemeSongGenerationEnabled { get; init; }
    public bool? SeekbarThumbnailGenerationEnabled { get; init; }
    public bool? ChapterExtractionEnabled { get; init; }
    public bool? MusicAudioAnalysisEnabled { get; init; }
    public bool? TranscodingEnabled { get; init; }
    public bool? TransmuxingEnabled { get; init; }
    public bool? RealtimeMonitorEnabled { get; init; }
    public int? AutoScanIntervalHours { get; init; }
}
