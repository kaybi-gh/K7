namespace K7.Server.Domain.Common;

/// <summary>
/// Maps ffprobe / device codec ids onto the names used in <c>MediaFormats</c>.
/// </summary>
public static class MediaCodecNames
{
    public static bool EqualsCodec(string? left, string? right) =>
        string.Equals(Canonical(left), Canonical(right), StringComparison.OrdinalIgnoreCase);

    public static string Canonical(string? codec)
    {
        if (string.IsNullOrWhiteSpace(codec))
            return string.Empty;

        var value = codec.Trim();
        if (value.StartsWith("pcm_", StringComparison.OrdinalIgnoreCase))
            return "pcm";

        return value.ToLowerInvariant() switch
        {
            "mpeg2video" or "mpeg2" => "mpeg2",
            "mpeg1video" or "mpeg1" => "mpeg1",
            "h265" or "hevc" => "hevc",
            "avc" or "h264" or "avc1" => "h264",
            "av01" or "av1" => "av1",
            "av02" or "av2" => "av2",
            "dca" or "dts" => "dts",
            "wma" or "wmav1" or "wmav2" => "wma",
            "mp4a" => "aac",
            _ => value.ToLowerInvariant()
        };
    }
}
