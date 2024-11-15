using System.Collections.Frozen;

namespace MediaServer.Domain.Constants;

public sealed record AudioQuality(string Name, int AverageBitrate, int MaxBitrate);
public sealed record VideoResolution(string Name, int Width, int Height, int AverageBitrate, int MaxBitrate);

public static class Qualities
{
    public static readonly FrozenDictionary<AudioQualityIdentifier, AudioQuality> Audio = new Dictionary<AudioQualityIdentifier, AudioQuality>
    {
        { AudioQualityIdentifier.LowAac, new("LowAac",  128000, 128000 ) },
        { AudioQualityIdentifier.MediumAac, new("MediumAac",  256000, 256000 ) },
        { AudioQualityIdentifier.HighAac, new("HighAac",  360000, 360000 ) },
    }.ToFrozenDictionary();

    public static readonly FrozenDictionary<VideoResolutionIdentifier, VideoResolution> Video = new Dictionary<VideoResolutionIdentifier, VideoResolution>
    {
        { VideoResolutionIdentifier._144p, new("144p", 256, 144, 150000, 200000) },
        { VideoResolutionIdentifier._240p, new("240p", 426, 240, 400000, 500000) },
        { VideoResolutionIdentifier._360p, new("360p", 640, 360, 800000, 1000000) },
        { VideoResolutionIdentifier._480p, new("480p", 854,  480, 1200000, 1500000) },
        { VideoResolutionIdentifier._720p, new("720p", 1280, 720, 2800000, 3500000) },
        { VideoResolutionIdentifier._1080p, new("1080p", 1920, 1080, 5000000, 6000000) },
        { VideoResolutionIdentifier._1440p, new("1440p", 2560, 1440, 10000000, 12000000) },
        { VideoResolutionIdentifier._2160p, new("2160p", 3840, 2160, 20000000, 25000000) },
        { VideoResolutionIdentifier._4320p, new("4320p", 7680, 4320, 50000000, 60000000) }
    }.ToFrozenDictionary();
}
