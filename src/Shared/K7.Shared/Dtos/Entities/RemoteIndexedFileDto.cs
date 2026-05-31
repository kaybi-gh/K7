using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record RemoteIndexedFileDto
{
    public required Guid Id { get; init; }
    public required Guid PeerServerId { get; init; }
    public required Guid RemoteFileId { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required long Size { get; init; }
    public required Guid RemoteMediaId { get; init; }
    public string? Container { get; init; }
    public TimeSpan? Duration { get; init; }
    public long? VideoBitrate { get; init; }
    public VideoResolutionIdentifier? VideoResolution { get; init; }
}
