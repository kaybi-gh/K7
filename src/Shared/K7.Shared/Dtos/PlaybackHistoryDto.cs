namespace K7.Shared.Dtos;

public sealed record PlaybackHistoryItemDto
{
    public Guid ReferenceId { get; init; }
    public Guid MediaId { get; init; }
    public string? MediaTitle { get; init; }
    public string? MediaType { get; init; }
    public string? ImageUrl { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? StoppedAt { get; init; }
    public double TotalWatchedSeconds { get; init; }
    public int SegmentCount { get; init; }
    public string? DeviceName { get; init; }
    public bool IsCompleted { get; init; }
    public string? UserName { get; init; }
    public StreamQualityDto? StreamQuality { get; init; }
}

public sealed record StreamQualityDto
{
    public bool? IsTranscode { get; init; }
    public string? Resolution { get; init; }
    public string? VideoCodec { get; init; }
    public string? AudioCodec { get; init; }
    public int? Bitrate { get; init; }
}

public sealed record PlaybackHistoryPageDto
{
    public IReadOnlyList<PlaybackHistoryItemDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
