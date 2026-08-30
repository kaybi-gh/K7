namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Formats Windows LibVLC can Direct Play well (D3D11VA when the GPU has the
/// decoder, avcodec otherwise). Broader than WebView2 MSE so MKV / HEVC / EAC3
/// stay muxed. Does not advertise every software-only VLC codec.
/// </summary>
public static class LibVlcWindowsCapabilities
{
    public static readonly string[] VideoCodecs =
    [
        "h264",
        "hevc",
        "mpeg2",
        "mpeg4",
        "vp8",
        "vp9",
        "av1"
    ];

    public static readonly string[] AudioCodecs =
    [
        "aac",
        "mp3",
        "opus",
        "vorbis",
        "flac",
        "ac3",
        "eac3",
        "dts",
        "truehd",
        "pcm",
        "wma",
        "alac",
        "mp2"
    ];

    private static readonly Dictionary<string, string[]> ContainerToRequiredCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mp4"] = ["h264", "hevc", "aac"],
        ["matroska"] = ["h264", "hevc", "aac", "eac3"],
        ["webm"] = ["vp9", "av1", "opus"],
        ["mpegts"] = ["h264", "hevc", "mpeg2"],
        ["mp3"] = ["mp3"],
        ["ogg"] = ["vorbis", "opus"],
        ["flac"] = ["flac"],
        ["wav"] = ["pcm"],
        ["avi"] = ["h264", "mpeg4", "mp3"],
        ["mov"] = ["h264", "hevc", "aac"],
        ["m4v"] = ["h264", "hevc", "aac"],
        ["3gp"] = ["h264", "aac"],
        ["mpeg"] = ["mpeg2"],
        ["aac"] = ["aac"],
        ["asf"] = ["wma"],
        ["flv"] = ["h264", "aac"]
    };

    public static string[] GetContainers()
    {
        var codecs = new HashSet<string>(VideoCodecs, StringComparer.OrdinalIgnoreCase);
        codecs.UnionWith(AudioCodecs);
        return GetContainers(codecs);
    }

    public static string[] GetContainers(IReadOnlyCollection<string> availableCodecs)
    {
        var set = new HashSet<string>(availableCodecs, StringComparer.OrdinalIgnoreCase);
        return ContainerToRequiredCodecs
            .Where(kv => kv.Value.Any(set.Contains))
            .Select(kv => kv.Key)
            .ToArray();
    }
}
