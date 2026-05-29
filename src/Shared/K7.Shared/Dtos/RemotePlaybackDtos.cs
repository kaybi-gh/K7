namespace K7.Shared.Dtos;

public enum RemoteTransportAction
{
    Play,
    Pause,
    Stop,
    SeekTo,
    SetVolume,
    SetAudioTrack,
    SetSubtitleTrack
}

public enum RemotePlaybackState
{
    Playing,
    Paused,
    Buffering,
    Stopped
}

public sealed record RemoteTransportCommandDto
{
    public required RemoteTransportAction Action { get; init; }
    public double? Value { get; init; }
    public int? TrackIndex { get; init; }
}

public sealed record RemotePlaybackRequestDto
{
    public required Guid IndexedFileId { get; init; }
    public double? StartPosition { get; init; }
    public bool IsAudio { get; init; }
    public Guid? MediaId { get; init; }
    public string? Title { get; init; }
    public string? Artist { get; init; }
    public string? AlbumTitle { get; init; }
    public string? CoverUrl { get; init; }
    public double? Duration { get; init; }
    public Guid? SenderDeviceId { get; init; }
}

public sealed record RemotePlaybackStateDto
{
    public RemotePlaybackState State { get; init; }
    public double Position { get; init; }
    public double Duration { get; init; }
    public double Volume { get; init; }
    public int? SelectedAudioTrackIndex { get; init; }
    public int? SelectedSubtitleTrackIndex { get; init; }
    public IReadOnlyList<RemoteTrackInfoDto>? AudioTracks { get; init; }
    public IReadOnlyList<RemoteTrackInfoDto>? SubtitleTracks { get; init; }
}

public sealed record RemoteTrackInfoDto
{
    public required int Index { get; init; }
    public required string Label { get; init; }
}
