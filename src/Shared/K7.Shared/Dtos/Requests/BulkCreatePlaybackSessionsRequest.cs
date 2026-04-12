namespace K7.Shared.Dtos.Requests;

public sealed record BulkCreatePlaybackSessionsRequest
{
    public required IReadOnlyList<PlaybackSessionItem> Items { get; init; }

    public sealed record PlaybackSessionItem
    {
        public required Guid MediaId { get; init; }
        public required DateTime StartedAt { get; init; }
        public double DurationSeconds { get; init; }
        public bool IsCompleted { get; init; }
        public Guid? ReferenceId { get; init; }
        public Guid? DeviceId { get; init; }
        public double? WatchedDurationSeconds { get; init; }
        public bool? IsTranscode { get; init; }
        public string? VideoDecision { get; init; }
        public string? AudioDecision { get; init; }
        public int? Bitrate { get; init; }
        public string? SourceVideoCodec { get; init; }
        public string? SourceAudioCodec { get; init; }
        public int? SourceVideoWidth { get; init; }
        public int? SourceVideoHeight { get; init; }
        public string? StreamVideoCodec { get; init; }
        public string? StreamAudioCodec { get; init; }
    }
}
