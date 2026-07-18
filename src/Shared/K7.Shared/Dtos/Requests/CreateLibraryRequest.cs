using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Requests;

public sealed record CreateLibraryRequest
{
    public required string Title { get; init; }
    public required LibraryMediaType MediaType { get; init; }
    public required string RootPath { get; init; }
    public required string MetadataProviderName { get; init; }
    public required string MetadataLanguage { get; init; }
    public required string MetadataFallbackLanguage { get; init; }
    public bool TriggerFileIndexingOnCreation { get; init; } = true;
    public Guid? LibraryGroupId { get; init; }
    public string? GroupTitle { get; init; }
    public string? GroupDescription { get; init; }
    public string? GroupIcon { get; init; }
    public bool? IntroDetectionEnabled { get; init; }
    public bool? ThemeSongGenerationEnabled { get; init; }
    public bool? SeekbarThumbnailGenerationEnabled { get; init; }
    public bool? ChapterExtractionEnabled { get; init; }
    public bool? MusicAudioAnalysisEnabled { get; init; }
    public bool? TranscodingEnabled { get; init; }
    public bool? TransmuxingEnabled { get; init; }
    public int? MetadataRefreshIntervalDays { get; init; }
    public bool RealtimeMonitorEnabled { get; init; } = true;
    public int AutoScanIntervalHours { get; init; } = 6;
}
