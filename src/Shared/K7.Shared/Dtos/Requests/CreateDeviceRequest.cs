using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Devices;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Shared.Dtos.Requests;
public sealed record CreateDeviceRequest
{
    public string? DeviceUniqueId { get; init; }
    public string? DeviceName { get; init; }
    public ClientType ClientType { get; init; }
    public DeviceType DeviceType { get; init; }
    public OperatingSystem OperatingSystem { get; init; }
    public string? OperatingSystemVersion { get; init; }
    public double DisplayHeight { get; init; }
    public double DisplayWidth { get; init; }
    public NativeDeviceDetailsDto? NativeDeviceDetails { get; init; }
    public WebDeviceDetailsDto? WebDeviceDetails { get; init; }
    public required CreateDeviceRequestPlaybackCapibilities PlaybackCapabilities { get; init; }
}

public sealed record CreateDeviceRequestPlaybackCapibilities
{
    public IEnumerable<string>? SupportedMediaFormatIds { get; init; }
    public IEnumerable<string>? SupportedSubtitlesCodecs { get; init; }
    public bool SupportsHDR { get; init; }
}
