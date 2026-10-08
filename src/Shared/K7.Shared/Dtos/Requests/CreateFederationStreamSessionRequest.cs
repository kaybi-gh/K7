using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Devices;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Shared.Dtos.Requests;

public sealed record CreateFederationStreamSessionRequest
{
    public required Guid IndexedFileId { get; init; }
    public required DevicePlaybackCapabilitiesDto DeviceCapabilities { get; init; }
    public int? AudioTrackIndex { get; init; }
    public int? SubtitleTrackIndex { get; init; }

    /// <summary>
    /// Real player. Unknown is treated as Web so an older requester still remuxes.
    /// Native is required for Direct Play (LibVLC / AVPlayer).
    /// </summary>
    public ClientType ClientType { get; init; }

    public OperatingSystem OperatingSystem { get; init; }
}
