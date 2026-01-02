using K7.Server.Domain.Entities.Devices;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Shared.Dtos.Entities;
public sealed record DeviceDto
{
    public Guid Id { get; init; }
    public string? DeviceUniqueId { get; init; }
    public string? DeviceName { get; init; }
    public DeviceType DeviceType { get; init; }
    public OperatingSystem OperatingSystem { get; init; }
    public string? OperatingSystemVersion { get; init; }
    public double DisplayHeight { get; init; }
    public double DisplayWidth { get; init; }
    public IEnumerable<MediaFormatDto> SupportedMediaFormats { get; init; } = [];
    public IEnumerable<string> SupportedSubtitlesCodecs { get; init; } = [];
    public bool SupportsHDR { get; init; }
    public DateTimeOffset LastSeen { get; init; }

    public static DeviceDto FromDomain(Device domain) => new()
    {
        Id = domain.Id,
        DeviceUniqueId = domain.DeviceUniqueId,
        DeviceName = domain.DeviceName,
        DeviceType = domain.DeviceType,
        OperatingSystem = domain.OperatingSystem,
        OperatingSystemVersion = domain.OperatingSystemVersion,
        DisplayHeight = domain.DisplayHeight,
        DisplayWidth = domain.DisplayWidth,
        SupportedMediaFormats = domain.SupportedMediaFormats.Select(MediaFormatDto.FromDomain),
        SupportedSubtitlesCodecs = domain.SupportedSubtitlesCodecs,
        SupportsHDR = domain.SupportsHDR,
        LastSeen = domain.LastSeen
    };
}
