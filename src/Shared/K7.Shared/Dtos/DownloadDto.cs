using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos;

public sealed record DownloadDto
{
    public required Guid Id { get; init; }
    public required Guid IndexedFileId { get; init; }
    public required Guid DeviceId { get; init; }
    public required DownloadStatus Status { get; init; }
    public bool IsDirectStream { get; init; }
    public long? FileSize { get; init; }
    public string? ContentType { get; init; }
    public DateTimeOffset? ReadyAt { get; init; }
    public string? FailureReason { get; init; }
}
