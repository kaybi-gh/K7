using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;
using System.Text.Json.Serialization;

namespace K7.Shared.Dtos.Entities.Metadatas.Files;

[JsonDerivedType(typeof(AudioFileMetadataDto), nameof(AudioFileMetadata))]
[JsonDerivedType(typeof(VideoFileMetadataDto), nameof(VideoFileMetadata))]
public abstract record FileMetadataDto
{
    public Guid Id { get; init; }
    public required string Container { get; init; }

    public static FileMetadataDto FromDomain(BaseFileMetadata domain) => domain switch
    {
        AudioFileMetadata audioFileMetadata => new AudioFileMetadataDto()
        {
            Id = domain.Id,
            Container = domain.Container,
            Duration = audioFileMetadata.Duration,
            AudioTrack = audioFileMetadata.AudioTrack != null ? AudioFileTrackDto.FromDomain(audioFileMetadata.AudioTrack) : null
        },
        VideoFileMetadata videoFileMetadata => new VideoFileMetadataDto()
        {
            Id = domain.Id,
            Container = domain.Container,
            Duration = videoFileMetadata.Duration,
            AudioTracks = videoFileMetadata.AudioTracks.Select(AudioFileTrackDto.FromDomain).ToList(),
            VideoTracks = videoFileMetadata.VideoTracks.Select(VideoFileTrackDto.FromDomain).ToList(),
            SubtitleTracks = videoFileMetadata.SubtitleTracks.Select(SubtitleFileTrackDto.FromDomain).ToList(),
            VideoBitrate = videoFileMetadata.VideoBitrate,
            VideoResolution = videoFileMetadata.VideoResolution,
            Thumbnails = videoFileMetadata.Thumbnails != null ? MetadataPictureDto.FromDomain(videoFileMetadata.Thumbnails) : null
        },
        _ => throw new NotSupportedException($"Unknown type: {domain.GetType().Name}")
    };
}
