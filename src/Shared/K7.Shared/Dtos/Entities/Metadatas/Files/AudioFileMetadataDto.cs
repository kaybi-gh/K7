using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Shared.Dtos.Entities.Metadatas.Files;
public sealed record AudioFileMetadataDto : FileMetadataDto
{
    public AudioFileTrackDto? AudioTrack {  get; init; }
    public TimeSpan Duration { get; init; }
}
