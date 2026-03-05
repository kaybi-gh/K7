namespace K7.Clients.Shared.Domain.Models;

public class PlayerSource
{
    public string? Url { get; set; }
    public string? MimeType { get; set; }
    public double? PendingSeekTime { get; set; }
    public string? ThumbnailsUrl { get; set; }
}
