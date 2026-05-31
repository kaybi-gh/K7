namespace K7.Shared.Dtos.Requests;

public sealed record CreateRemoteStreamSessionRequest
{
    public required Guid RemoteFileId { get; init; }
    public required Guid DeviceId { get; init; }
    public int? AudioTrackIndex { get; init; }
}
