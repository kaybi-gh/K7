using K7.Server.Domain.Entities.Federation;

namespace K7.Server.Domain.Entities;

public class Library : BaseAuditableEntity
{
    public required string Title { get; set; }
    public required LibraryMediaType MediaType { get; set; }
    public string? RootPath { get; set; }
    public required string MetadataProviderName { get; set; }
    public required string MetadataLanguage { get; set; }
    public required string MetadataFallbackLanguage { get; set; }
    public int? MetadataRefreshIntervalDays { get; set; }
    public bool? RootPathAccessible { get; set; }

    public bool IntroDetectionEnabled { get; set; } = true;
    public bool SeekbarThumbnailGenerationEnabled { get; set; } = true;
    public bool ChapterExtractionEnabled { get; set; } = true;
    public bool MusicAudioAnalysisEnabled { get; set; } = true;
    public bool TranscodingEnabled { get; set; } = true;
    public bool TransmuxingEnabled { get; set; } = true;

    public bool RealtimeMonitorEnabled { get; set; } = true;
    public int AutoScanIntervalHours { get; set; } = 6;

    public required Guid LibraryGroupId { get; set; }
    public LibraryGroup? LibraryGroup { get; set; }

    public Guid? PeerServerId { get; set; }
    public PeerServer? PeerServer { get; set; }

    public IList<IndexedFile> IndexedFiles { get; set; } = [];
    public IList<LibraryScanIssue> ScanIssues { get; set; } = [];
    public IList<RemoteIndexedFile> RemoteIndexedFiles { get; set; } = [];
}
