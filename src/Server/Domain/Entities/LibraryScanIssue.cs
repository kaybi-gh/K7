namespace K7.Server.Domain.Entities;

public class LibraryScanIssue : BaseEntity
{
    public required Guid LibraryId { get; set; }
    public required string Path { get; set; }
    public required string ErrorMessage { get; set; }
    public required DateTimeOffset DetectedAt { get; set; }
}
