using MediaServer.Domain.Entities.Metadatas.Files.Tracks;

namespace MediaServer.Domain.Entities.Metadatas.Files;
public class AudioFileMetadata() : BaseFileMetadata(FileType.Audio)
{
    public AudioFileTrack? AudioTrack {  get; set; }
}
