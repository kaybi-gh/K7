using K7.Shared.Dtos;

namespace K7.Shared.Interfaces;

public interface IRemotePlaybackClient
{
    Task ReceiveRemotePlaybackRequest(RemotePlaybackRequestDto request);
    Task ReceiveRemoteTransportCommand(RemoteTransportCommandDto command);
    Task ReceiveConnectedDevicesUpdated(IReadOnlyList<ConnectedDeviceDto> devices);
    Task ReceiveRemotePlaybackState(RemotePlaybackStateDto state);
}

public sealed record ConnectedDeviceDto
{
    public required Guid DeviceId { get; init; }
    public required string DeviceName { get; init; }
    public required string DeviceType { get; init; }
}
