using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Devices;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Shared.Dtos.Devices;

public record DevicePlaybackCapabilitiesDto
{
    public required IReadOnlyList<MediaFormatDto> SupportedMediaFormats { get; init; }
    public required IReadOnlyList<string> SupportedSubtitlesCodecs { get; init; }
    public bool SupportsHDR { get; init; }

    public static DevicePlaybackCapabilitiesDto FromDomain(DevicePlaybackCapabilities domain) => new()
    {
        SupportedMediaFormats = Constants.MediaFormats
            .Where(x => domain.SupportedMediaFormatIds.Contains(x.Id))
            .Select(MediaFormatDto.FromDomain)
            .ToList(),
        SupportedSubtitlesCodecs = domain.SupportedSubtitlesCodecs.ToList(),
        SupportsHDR = domain.SupportsHDR,
    };
}
