using K7.Server.Domain.Enums;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Shared.Dtos.Requests;
public sealed record CreateDeviceRequest
{
    public string? DeviceUniqueId { get; init; }
    public string? DeviceName { get; init; }
    public DeviceType DeviceType { get; init; }
    public OperatingSystem OperatingSystem { get; init; }
    public string? OperatingSystemVersion { get; init; }
    public double DisplayWidth { get; init; }
    public double DisplayHeight { get; init; }
    public IEnumerable<string>? SupportedMediaFormatIds { get; init; }
    public IEnumerable<string>? SupportedSubtitlesCodecs { get; init; }
    public bool SupportsHDR { get; init; }
}
