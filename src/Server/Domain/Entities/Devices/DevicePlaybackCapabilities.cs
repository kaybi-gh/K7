using K7.Server.Domain.Entities.MediaFormats;

namespace K7.Server.Domain.Entities.Devices;

public class DevicePlaybackCapabilities
{
    public IList<string> SupportedMediaFormatIds { get; set; } = [];
    public IList<string> SupportedSubtitlesCodecs { get; set; } = [];
    public bool SupportsHDR { get; set; }

    public IReadOnlyList<BaseMediaFormat> SupportedMediaFormats
        => [.. Constants.Constants.MediaFormats.Where(x => SupportedMediaFormatIds.Contains(x.Id))];
}

