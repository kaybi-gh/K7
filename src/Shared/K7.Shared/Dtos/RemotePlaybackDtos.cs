using K7.Shared.Dtos.Entities.Metadatas.Files;

namespace K7.Shared.Dtos;

public enum RemoteTransportAction
{
    Play,
    Pause,
    Stop,
    SeekTo,
    SetVolume,
    SetAudioTrack,
    SetSubtitleTrack,
    ReleaseControl,
    SetPlaybackRate,
    SetAspectRatio
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
    /// <summary>Sender playback volume (0-1) so the receiver and remote UI start aligned.</summary>
    public double? Volume { get; init; }
    /// <summary>
    /// When true, the target keeps its current playback and only starts reporting
    /// state to the sender (companion remote). When false, the target starts or
    /// replaces playback with this request.
    /// </summary>
    public bool AttachOnly { get; init; }
    /// <summary>Seek-preview sprite sheet URL so the companion seekbar can show thumbnails.</summary>
    public string? ThumbnailsUrl { get; init; }
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
    public string? ThumbnailsUrl { get; init; }
    public IReadOnlyList<ChapterMarkerDto>? Chapters { get; init; }
    public double PlaybackRate { get; init; } = 1;
    public int AspectRatio { get; init; }
}

public sealed record RemoteTrackInfoDto
{
    public required int Index { get; init; }
    public required string Label { get; init; }
    public string? Language { get; init; }
    public string? Name { get; init; }
    public string? Codec { get; init; }
    public string? ChannelLayout { get; init; }
    public bool IsForced { get; init; }
    public bool IsHearingImpaired { get; init; }
}

public sealed record PlaybackTakenOverDto
{
    public Guid NewDeviceId { get; init; }
    public string NewDeviceName { get; init; } = "Device";
    public string? Title { get; init; }
    public Guid? MediaId { get; init; }
    public Guid? IndexedFileId { get; init; }
    public bool IsAudio { get; init; }
    public string? CoverUrl { get; init; }
    public double Position { get; init; }
    public double Duration { get; init; }
}
