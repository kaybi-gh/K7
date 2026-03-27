using K7.Shared.Dtos.Entities.Medias;

namespace K7.Shared.Dtos.Devices;

public record DevicePlaybackCapabilitiesDto
{
    public required IReadOnlyList<MediaFormatDto> SupportedMediaFormats { get; init; }
    public required IReadOnlyList<string> SupportedSubtitlesCodecs { get; init; }
    public bool SupportsHDR { get; init; }

}
