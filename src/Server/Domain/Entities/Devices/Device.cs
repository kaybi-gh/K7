using K7.Server.Domain.Entities.MediaFormats;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Server.Domain.Entities.Devices;
public class Device : BaseAuditableEntity
{
    public string? DeviceUniqueId { get; set; }
    public string? DeviceName { get; set; }
    public DeviceType DeviceType { get; set; } = DeviceType.Unknown;
    public OperatingSystem OperatingSystem { get; set; } = OperatingSystem.Unknown;
    public string? OperatingSystemVersion { get; set; }
    public double DisplayHeight { get; set; }
    public double DisplayWidth { get; set; }
    public ICollection<string> SupportedMediaFormatIds { get; set; } = [];
    public ICollection<string> SupportedSubtitlesCodecs { get; set; } = [];
    public bool SupportsHDR { get; set; }
    public DateTimeOffset LastSeen { get; set; }
    //public ICollection<User> Users { get; set; } = []; // TODO - Link to users?

    public ICollection<BaseMediaFormat> SupportedMediaFormats
    {
        get => [.. Constants.Constants.MediaFormats.Where(x => SupportedMediaFormatIds.Contains(x.Id))];
        set => SupportedMediaFormatIds = value?.Select(mf => mf.Id).ToList() ?? [];
    }
}
