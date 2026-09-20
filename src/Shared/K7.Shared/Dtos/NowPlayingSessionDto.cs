namespace K7.Shared.Dtos;

public sealed record NowPlayingSessionDto
{
    public Guid SessionId { get; init; }
    public Guid? DeviceId { get; init; }
    public string? DeviceName { get; init; }
    public string? DeviceType { get; init; }
    public string? DeviceClient { get; init; }
    public Guid? MediaId { get; init; }
    public Guid? IndexedFileId { get; init; }
    public string? MediaTitle { get; init; }
    public string? MediaType { get; init; }
    public Guid? ParentId { get; init; }
    public int? SeasonNumber { get; init; }
    public int? EpisodeNumber { get; init; }
    public string? ThumbnailUrl { get; init; }
    public double Position { get; init; }
    public double Duration { get; init; }
    public int State { get; init; }
    public bool IsAudio { get; init; }
    public bool CanControl { get; init; }
    public int? AudioTrackIndex { get; init; }
    public int? SubtitleTrackIndex { get; init; }
    public double PlaybackRate { get; init; } = 1;
}
