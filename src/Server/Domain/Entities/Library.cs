namespace K7.Server.Domain.Entities;

public class Library : BaseAuditableEntity
{
    public required string Title { get; set; }
    public required LibraryMediaType MediaType { get; set; }
    public required string RootPath { get; set; }
    public required string MetadataProviderName { get; set; }
    public required string MetadataLanguage { get; set; }
    public required string MetadataFallbackLanguage { get; set; }
    public int? MetadataRefreshIntervalDays { get; set; }
    public bool? RootPathAccessible { get; set; }
    public string? Description { get; set; }
    public string? Icon { get; set; }

    public MetadataPicture? CoverPicture { get; set; }
    public IList<IndexedFile> IndexedFiles { get; set; } = [];
    public IList<LibraryScanIssue> ScanIssues { get; set; } = [];
}
