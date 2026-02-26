namespace K7.Shared.Dtos.Requests;

public sealed record CreateStreamSessionRequest
{
    public required Guid IndexedFileId { get; init; }
    public required Guid DeviceId { get; init; }
    public int? AudioTrackIndex { get; init; }
}
