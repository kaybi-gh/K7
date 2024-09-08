namespace MediaServer.Domain.Entities.Metadatas.Files;
public class VideoFileMetadata() : BaseFileMetadata(FileType.Video)
{
    public IEnumerable<HlsSegment> HlsSegments { get; set; } = [];
    public IEnumerable<MetadataPicture> Thumbnails { get; set; } = [];
    /*public VideoInfo VideoInfo { get; set; } = null!;
    public List<AudioTrack> AudioTracks { get; set; } = null!;

    public class AudioTrack
    {
        public int Index { get; set; }
        public string? Language { get; set; }
        public string? Title { get; set; }
        public bool IsDefault { get; set; }
    }*/
}
