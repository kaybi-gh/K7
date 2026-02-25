using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos;

public class StreamingSessionDto
{
    public required Guid Id { get; set; } = Guid.NewGuid();
    public required Guid IndexedFileId { get; set; }
    public PlaybackState State { get; set; }
    public double Position { get; set; }
    public required PlaybackSettingsDto PlaybackSettings { get; set; }

    /// <summary>
    /// Initial stream URL and MIME type selected for this session
    /// (e.g. direct-play or HLS manifest in MP4/HEVC).
    /// </summary>
    public IndexedFileStreamUri? Source { get; set; }
}

