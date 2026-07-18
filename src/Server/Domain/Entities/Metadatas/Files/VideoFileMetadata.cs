using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;

namespace K7.Server.Domain.Entities.Metadatas.Files;
public class VideoFileMetadata() : BaseFileMetadata(FileType.Video)
{
    public required long VideoBitrate { get; set; }
    public TimeSpan Duration { get; set; }
    public required VideoResolutionIdentifier VideoResolution { get; set; }

    public ICollection<AudioFileTrack> AudioTracks { get; set; } = [];
    public ICollection<VideoFileTrack> VideoTracks { get; set; } = [];
    public ICollection<SubtitleFileTrack> SubtitleTracks { get; set; } = [];
    public MetadataPicture? Thumbnails { get; set; }
    public IList<HlsSegment> HlsSegments { get; set; } = [];

    /// <summary>
    /// Null = never extracted; empty = extracted with no chapters; otherwise chapter markers.
    /// </summary>
    public List<ChapterMarker>? Chapters { get; set; }
}
