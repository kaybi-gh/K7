using K7.Server.Domain.Entities.Metadatas.Files.Tracks;

namespace K7.Server.Domain.Entities.Metadatas.Files;
public class VideoFileMetadata() : BaseFileMetadata(FileType.Video)
{
    public required long VideoBitrate { get; set; }
    public required VideoResolutionIdentifier VideoResolution { get; set; }

    public ICollection<AudioFileTrack> AudioTracks { get; set; } = [];
    public ICollection<VideoFileTrack> VideoTracks { get; set; } = [];
    public ICollection<MetadataPicture> Thumbnails { get; set; } = [];
}
