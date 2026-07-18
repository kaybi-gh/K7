using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Metadatas.Files;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Shared.Dtos;

public class StreamingSessionDto
{
    public required Guid Id { get; set; } = Guid.NewGuid();
    public required Guid IndexedFileId { get; set; }
    public PlaybackState State { get; set; }
    public double Position { get; set; }
    public required PlaybackSettingsDto PlaybackSettings { get; set; }

    public IReadOnlyList<AudioFileTrackDto> AudioTracks { get; set; } = [];
    public IReadOnlyList<SubtitleFileTrackDto> SubtitleTracks { get; set; } = [];
    public IReadOnlyList<ChapterMarkerDto>? Chapters { get; set; }

    /// <summary>
    /// Initial stream URL and MIME type selected for this session
    /// (e.g. direct-play or HLS manifest in MP4/HEVC).
    /// </summary>
    public IndexedFileStreamUri? Source { get; set; }
}

