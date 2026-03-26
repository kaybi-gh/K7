using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Metadatas.Files.Tracks;

namespace K7.Shared.Dtos.Entities.Metadatas.Files;
public sealed record VideoFileMetadataDto : FileMetadataDto
{
    public required long VideoBitrate { get; init; }
    public TimeSpan Duration { get; init; }
    public required VideoResolutionIdentifier VideoResolution { get; init; }

    public IReadOnlyList<AudioFileTrackDto> AudioTracks { get; init; } = [];
    public IReadOnlyList<VideoFileTrackDto> VideoTracks { get; init; } = [];
    public IReadOnlyList<SubtitleFileTrackDto> SubtitleTracks { get; init; } = [];
    public MetadataPictureDto? Thumbnails { get; init; }
}
