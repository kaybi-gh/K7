using K7.Shared.Dtos.Devices;

namespace K7.Shared.Dtos.Requests;

public sealed record CreateFederationStreamSessionRequest
{
    public required Guid IndexedFileId { get; init; }
    public required DevicePlaybackCapabilitiesDto DeviceCapabilities { get; init; }
    public int? AudioTrackIndex { get; init; }
}
