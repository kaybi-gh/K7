using System.Collections.Frozen;

namespace MediaServer.Domain.Constants;

public sealed record VideoQuality(string Name, int Width, int Height, int AverageBitrate, int MaxBitrate);

public static class Qualities
{
    public static readonly FrozenDictionary<VideoQualityIdentifier, VideoQuality> Video = new Dictionary<VideoQualityIdentifier, VideoQuality>
    {
        { VideoQualityIdentifier._144p, new("144p", 256, 144, 150000, 200000) },
        { VideoQualityIdentifier._240p, new("240p", 426, 240, 400000, 500000) },
        { VideoQualityIdentifier._360p, new("360p", 640, 360, 800000, 1000000) },
        { VideoQualityIdentifier._480p, new("480p", 854,  480, 1200000, 1500000) },
        { VideoQualityIdentifier._720p, new("720p", 1280, 720, 2800000, 3500000) },
        { VideoQualityIdentifier._1080p, new("1080p", 1920, 1080, 5000000, 6000000) },
        { VideoQualityIdentifier._1440p, new("1440p", 2560, 1440, 10000000, 12000000) },
        { VideoQualityIdentifier._2160p, new("2160p", 3840, 2160, 20000000, 25000000) },
        { VideoQualityIdentifier._4320p, new("4320p", 7680, 4320, 50000000, 60000000) }
    }.ToFrozenDictionary();
}
