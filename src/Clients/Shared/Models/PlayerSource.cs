namespace K7.Clients.Shared.Models;

public class PlayerSource
{
    public Guid? MediaId { get; set; }
    public string? Url { get; set; }
    public string? MimeType { get; set; }
    public double? PendingSeekTime { get; set; }
    public string? ThumbnailsUrl { get; set; }
}
