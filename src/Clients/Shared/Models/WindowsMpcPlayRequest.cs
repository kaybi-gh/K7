namespace K7.Clients.Shared.Models;

public sealed record WindowsMpcPlayRequest
{
    public required Guid IndexedFileId { get; init; }
    public Guid? MediaId { get; init; }
    public string? Title { get; init; }
    public string? CoverUrl { get; init; }
    public double? StartPositionSeconds { get; init; }
    public double? DurationSeconds { get; init; }
    public int? AudioTrackIndex { get; init; }
    public int? SubtitleTrackIndex { get; init; }
}
