using K7.Server.Domain.Entities.MediaFormats;

namespace K7.Server.Domain.Entities.Devices;

public class DevicePlaybackCapabilities
{
    public ICollection<string> SupportedMediaFormatIds { get; set; } = [];
    public ICollection<string> SupportedSubtitlesCodecs { get; set; } = [];
    public bool SupportsHDR { get; set; }

    public ICollection<BaseMediaFormat> SupportedMediaFormats
        => [.. Constants.Constants.MediaFormats.Where(x => SupportedMediaFormatIds.Contains(x.Id))];
}

