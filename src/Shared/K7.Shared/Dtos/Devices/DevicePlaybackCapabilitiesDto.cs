using K7.Server.Domain.Entities.Devices;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Shared.Dtos.Devices;

public record DevicePlaybackCapabilitiesDto
{
    public required IEnumerable<MediaFormatDto> SupportedMediaFormats { get; init; }
    public required ICollection<string> SupportedSubtitlesCodecs { get; init; }
    public bool SupportsHDR { get; init; }

    public static DevicePlaybackCapabilitiesDto FromDomain(DevicePlaybackCapabilities domain) => new()
    {
        SupportedMediaFormats = domain.SupportedMediaFormats.Select(MediaFormatDto.FromDomain),
        SupportedSubtitlesCodecs = domain.SupportedSubtitlesCodecs,
        SupportsHDR = domain.SupportsHDR,
    };
}
