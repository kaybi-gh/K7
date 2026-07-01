namespace K7.Import.Models;

public sealed record SourceMediaItem
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public int? Year { get; init; }
    public Dictionary<string, string> ProviderIds { get; init; } = [];
    public int PlayCount { get; init; }
    public double? LastPlaybackPosition { get; init; }
    public double? DurationSeconds { get; init; }
    public DateTime? LastPlayedAt { get; init; }
    public bool IsCompleted { get; init; }
    public double? Rating { get; init; }

    public string? MediaType { get; init; }
    public string? ArtistName { get; init; }
    public string? AlbumName { get; init; }
    public string? SeriesTitle { get; init; }
    public int? SeasonNumber { get; init; }
    public int? EpisodeNumber { get; init; }

    public List<SourcePlayEntry> PlayHistory { get; init; } = [];
}

public sealed record SourcePlayEntry
{
    public required DateTime PlayedAt { get; init; }
    public double DurationSeconds { get; init; }
    public bool IsCompleted { get; init; } = true;
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
    public string? DeviceName { get; init; }
    public string? Platform { get; init; }
    public string? Player { get; init; }
}
