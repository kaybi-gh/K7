namespace K7.Clients.Shared.Models;

public class PlayerSource
{
    public Guid? MediaId { get; set; }
    public Guid? StreamSessionId { get; set; }
    public Guid? IndexedFileId { get; set; }
    public string? Url { get; set; }
    public string? MimeType { get; set; }
    public double? PendingSeekTime { get; set; }
    public string? ThumbnailsUrl { get; set; }
    public string? Title { get; set; }
    public string? CoverUrl { get; set; }
}
