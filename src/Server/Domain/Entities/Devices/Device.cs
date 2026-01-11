using K7.Server.Domain.Entities.Users;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Server.Domain.Entities.Devices;
public class Device : BaseAuditableEntity
{
    public string? DeviceUniqueId { get; set; }
    public string? DeviceName { get; set; }
    public required ClientType ClientType { get; set; }
    public DeviceType DeviceType { get; set; } = DeviceType.Unknown;
    public OperatingSystem OperatingSystem { get; set; } = OperatingSystem.Unknown;
    public string? OperatingSystemVersion { get; set; }
    public double DisplayHeight { get; set; }
    public double DisplayWidth { get; set; }
    public NativeDeviceDetails? NativeDeviceDetails { get; set; }
    public WebDeviceDetails? WebDeviceDetails { get; set; }
    public DevicePlaybackCapabilities PlaybackCapabilities { get; set; } = new();
    public ICollection<User> Users { get; set; } = [];
    public DateTimeOffset LastSeen { get; set; }
}
