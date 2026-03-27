using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Users;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Shared.Dtos.Devices;
public sealed record DeviceDto
{
    public Guid Id { get; init; }
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
    public required DevicePlaybackCapabilitiesDto PlaybackCapabilities { get; init; }
    public required IReadOnlyList<LiteUserDto> Users { get; init; }
    public DateTimeOffset LastSeen { get; init; }

}
