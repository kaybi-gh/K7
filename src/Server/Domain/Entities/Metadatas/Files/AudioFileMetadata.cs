using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;

namespace K7.Server.Domain.Entities.Metadatas.Files;
public class AudioFileMetadata() : BaseFileMetadata(FileType.Audio)
{
    public AudioFileTrack? AudioTrack {  get; set; }
    public TimeSpan Duration { get; set; }
    public IList<HlsSegment> HlsSegments { get; set; } = [];
}
