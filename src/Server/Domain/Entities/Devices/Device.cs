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
    /// <summary>Logical screen size in CSS pixels (web) or DIP (native).</summary>
    public double DisplayScreenHeight { get; set; }
    /// <summary>Logical screen size in CSS pixels (web) or DIP (native).</summary>
    public double DisplayScreenWidth { get; set; }
    /// <summary>Physical pixel height (CSS * DPR, or DIP * density).</summary>
    public double DisplayResolutionHeight { get; set; }
    /// <summary>Physical pixel width (CSS * DPR, or DIP * density).</summary>
    public double DisplayResolutionWidth { get; set; }
    public NativeDeviceDetails? NativeDeviceDetails { get; set; }
    public WebDeviceDetails? WebDeviceDetails { get; set; }
    public DevicePlaybackCapabilities PlaybackCapabilities { get; set; } = new();
    public IList<User> Users { get; set; } = [];
    public DateTimeOffset LastSeen { get; set; }
}
