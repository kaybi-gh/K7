namespace K7.Server.Application.Common;

/// <summary>
/// Maps logical audio codecs to ffmpeg encoder names and AAC encode parameters.
/// Prefer aac_at / libfdk_aac when ffmpeg was built with them; otherwise built-in aac.
/// HLS / download AAC encode uses a fixed 256 kbps stereo target.
/// </summary>
public static class FfmpegAudioEncoderResolver
{
    public const int FixedAacBitrateBps = 256_000;
    public const int DefaultSampleRateHz = 48_000;
    public const int HlsStereoChannels = 2;

    /// <summary>
    /// Encoder priming in samples (ISO AAC-LC). Native ffmpeg aac is one frame;
    /// libfdk_aac and AudioToolbox need two frames / 2112.
    /// </summary>
    public static int GetAacEncoderDelaySamples(string encoderName)
    {
        if (string.Equals(encoderName, "libfdk_aac", StringComparison.OrdinalIgnoreCase))
            return 2048;

        if (string.Equals(encoderName, "aac_at", StringComparison.OrdinalIgnoreCase))
            return 2112;

        return 1024;
    }

    public static double GetAacEncoderDelaySeconds(string encoderName, int sampleRateHz)
    {
        var rate = sampleRateHz > 0 ? sampleRateHz : DefaultSampleRateHz;
        return GetAacEncoderDelaySamples(encoderName) / (double)rate;
    }

    public static string? ResolveEncoderName(
        string logicalCodec,
        IReadOnlyList<string>? availableEncoders = null)
    {
        if (string.IsNullOrWhiteSpace(logicalCodec))
            return null;

        return logicalCodec.ToLowerInvariant() switch
        {
            "aac" => ResolveAacEncoder(availableEncoders),
            "opus" => "libopus",
            "mp3" => "libmp3lame",
            "vorbis" => "libvorbis",
            "ac3" => "ac3",
            "eac3" => "eac3",
            "flac" => "flac",
            "alac" => "alac",
            _ => logicalCodec
        };
    }

    public static string ResolveAacEncoder(IReadOnlyList<string>? availableEncoders)
    {
        if (HasEncoder(availableEncoders, "aac_at"))
            return "aac_at";

        if (HasEncoder(availableEncoders, "libfdk_aac"))
            return "libfdk_aac";

        return "aac";
    }

    /// <summary>
    /// libfdk_aac VBR quality band from bitrate-per-channel.
    /// </summary>
    public static int GetLibfdkVbrMode(int bitrateBps, int channels)
    {
        var bitratePerChannel = bitrateBps / Math.Max(channels, 1);
        return bitratePerChannel switch
        {
            < 32_000 => 1,
            < 48_000 => 2,
            < 64_000 => 3,
            < 96_000 => 4,
            _ => 5
        };
    }

    /// <summary>
    /// Builds ffmpeg output-side AAC encode arguments (without map/vn).
    /// </summary>
    public static IReadOnlyList<string> BuildAacEncodeArguments(
        string encoderName,
        int? forceChannels = null,
        int? sampleRateHz = null,
        bool preferVbr = true)
    {
        var args = new List<string> { $"-c:a {encoderName}" };

        if (forceChannels is int channels and > 0)
            args.Add($"-ac {channels}");

        if (sampleRateHz is int rate and > 0)
            args.Add($"-ar {rate}");

        if (preferVbr
            && string.Equals(encoderName, "libfdk_aac", StringComparison.OrdinalIgnoreCase))
        {
            var vbrChannels = forceChannels is int c and > 0 ? c : HlsStereoChannels;
            args.Add($"-vbr:a {GetLibfdkVbrMode(FixedAacBitrateBps, vbrChannels)}");
        }
        else
        {
            args.Add($"-b:a {FixedAacBitrateBps}");
        }

        return args;
    }

    private static bool HasEncoder(IReadOnlyList<string>? availableEncoders, string encoderName)
    {
        if (availableEncoders is null || availableEncoders.Count == 0)
            return false;

        return availableEncoders.Contains(encoderName, StringComparer.OrdinalIgnoreCase);
    }
}
