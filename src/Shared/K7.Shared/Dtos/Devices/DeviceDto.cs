using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Enums;
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
    public required IEnumerable<UserDto> Users { get; init; } // TODO - LiteUserDto
    public DateTimeOffset LastSeen { get; init; }

    public static DeviceDto FromDomain(Device domain) => new()
    {
        Id = domain.Id,
        DeviceUniqueId = domain.DeviceUniqueId,
        DeviceName = domain.DeviceName,
        ClientType = domain.ClientType,
        DeviceType = domain.DeviceType,
        OperatingSystem = domain.OperatingSystem,
        OperatingSystemVersion = domain.OperatingSystemVersion,
        DisplayHeight = domain.DisplayHeight,
        DisplayWidth = domain.DisplayWidth,
        NativeDeviceDetails = NativeDeviceDetailsDto.FromDomain(domain.NativeDeviceDetails),
        WebDeviceDetails = WebDeviceDetailsDto.FromDomain(domain.WebDeviceDetails),
        PlaybackCapabilities = DevicePlaybackCapabilitiesDto.FromDomain(domain.PlaybackCapabilities),
        Users = domain.Users.Select(UserDto.FromDomain),
        LastSeen = domain.LastSeen
    };
}
