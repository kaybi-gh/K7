namespace K7.Shared.Dtos;

public sealed record PlaybackHistoryItemDto
{
    public Guid ReferenceId { get; init; }
    public Guid MediaId { get; init; }
    public string? MediaTitle { get; init; }
    public string? MediaType { get; init; }
    public string? MediaUrl { get; init; }
    public string? LibraryName { get; init; }
    public string? ImageUrl { get; init; }
    public DateTime StartedAt { get; init; }
    public DateTime? StoppedAt { get; init; }
    public double TotalWatchedSeconds { get; init; }
    public int SegmentCount { get; init; }
    public string? DeviceName { get; init; }
    public string? DeviceClient { get; init; }
    public bool IsCompleted { get; init; }
    public string? UserName { get; init; }
    public string? ViewingGroupName { get; init; }
    public StreamQualityDto? StreamQuality { get; init; }
}

public sealed record StreamQualityDto
{
    public bool? IsTranscode { get; init; }
    public string? VideoDecision { get; init; }
    public string? AudioDecision { get; init; }
    public string? TranscodeReason { get; init; }
    public string? SourceResolution { get; init; }
    public string? SourceVideoCodec { get; init; }
    public string? SourceAudioCodec { get; init; }
    public string? StreamVideoCodec { get; init; }
    public string? StreamAudioCodec { get; init; }
    public string? Resolution { get; init; }
    public string? VideoCodec { get; init; }
    public string? AudioCodec { get; init; }
    public int? Bitrate { get; init; }
    public string? AudioTrackLanguage { get; init; }
    public string? AudioTrackTitle { get; init; }
    public string? AudioChannelLayout { get; init; }
    public string? SubtitleTrackLanguage { get; init; }
    public string? SubtitleTrackTitle { get; init; }
}

public sealed record PlaybackHistoryPageDto
{
    public IReadOnlyList<PlaybackHistoryItemDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}
