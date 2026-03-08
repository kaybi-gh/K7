using Android.Content;
using Android.Hardware.Display;
using Android.Media;
using Android.Views;
using K7.Clients.MAUI.Interfaces;

namespace K7.Clients.MAUI.Platforms.Android.Services;

public class CodecService : ICodecService
{
    private static readonly Dictionary<string, string> VideoMimeToCodec = new(StringComparer.OrdinalIgnoreCase)
    {
        ["video/avc"] = "h264",
        ["video/hevc"] = "hevc",
        ["video/x-vnd.on2.vp8"] = "vp8",
        ["video/x-vnd.on2.vp9"] = "vp9",
        ["video/av01"] = "av1",
        ["video/mp4v-es"] = "mpeg4",
        ["video/mpeg2"] = "mpeg2",
    };

    private static readonly Dictionary<string, string> AudioMimeToCodec = new(StringComparer.OrdinalIgnoreCase)
    {
        ["audio/mp4a-latm"] = "aac",
        ["audio/mpeg"] = "mp3",
        ["audio/opus"] = "opus",
        ["audio/vorbis"] = "vorbis",
        ["audio/flac"] = "flac",
        ["audio/ac3"] = "ac3",
        ["audio/eac3"] = "eac3",
        ["audio/raw"] = "pcm",
        ["audio/x-ms-wma"] = "wma",
    };

    private static readonly Dictionary<string, string[]> ContainerToRequiredCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mp4"] = ["h264", "aac"],
        ["matroska"] = ["h264", "aac"],
        ["webm"] = ["vp9", "opus"],
        ["mpegts"] = ["h264"],
        ["mp3"] = ["mp3"],
        ["ogg"] = ["vorbis"],
        ["flac"] = ["flac"],
        ["wav"] = ["pcm"],
        ["avi"] = ["h264", "mp3"],
        ["mov"] = ["h264", "aac"],
        ["aac"] = ["aac"],
        ["asf"] = ["wma"],
        ["flv"] = ["h264", "aac"],
    };

    public Task<bool> GetHdrSupportAsync()
    {
        var displayManager = (DisplayManager?)global::Android.App.Application.Context.GetSystemService(Context.DisplayService);
        var display = displayManager?.GetDisplay(Display.DefaultDisplay);

        if (display == null)
        {
            return Task.FromResult(false);
        }

#if ANDROID33_0_OR_GREATER
        return Task.FromResult(display.IsHdr);
#elif ANDROID26_0_OR_GREATER
        var hdrCapabilities = display.GetHdrCapabilities();
        return Task.FromResult(hdrCapabilities?.GetSupportedHdrTypes()?.Length > 0);
#else
    return Task.FromResult(false);
#endif
    }

    public Task<string[]> GetSupportedVideoCodecsAsync()
    {
        try
        {
            var codecs = GetSupportedDecoderCodecs(VideoMimeToCodec);
            return Task.FromResult(codecs);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[K7-Codec] Video codec detection failed: {ex}");
            return Task.FromResult(Array.Empty<string>());
        }
    }

    public Task<string[]> GetSupportedAudioCodecsAsync()
    {
        try
        {
            var codecs = GetSupportedDecoderCodecs(AudioMimeToCodec);
            return Task.FromResult(codecs);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[K7-Codec] Audio codec detection failed: {ex}");
            return Task.FromResult(Array.Empty<string>());
        }
    }

    public Task<string[]> GetSupportedContainersAsync()
    {
        try
        {
            var audioCodecs = new HashSet<string>(GetSupportedDecoderCodecs(AudioMimeToCodec), StringComparer.OrdinalIgnoreCase);
            var videoCodecs = new HashSet<string>(GetSupportedDecoderCodecs(VideoMimeToCodec), StringComparer.OrdinalIgnoreCase);
            var allCodecs = new HashSet<string>(audioCodecs, StringComparer.OrdinalIgnoreCase);
            allCodecs.UnionWith(videoCodecs);

            var containers = ContainerToRequiredCodecs
                .Where(kv => kv.Value.Any(c => allCodecs.Contains(c)))
                .Select(kv => kv.Key)
                .ToArray();

            return Task.FromResult(containers);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[K7-Codec] Container detection failed: {ex}");
            return Task.FromResult(Array.Empty<string>());
        }
    }

    private static string[] GetSupportedDecoderCodecs(Dictionary<string, string> mimeToCodecMap)
    {
        var codecList = new MediaCodecList(MediaCodecListKind.AllCodecs);
        var codecInfos = codecList.GetCodecInfos();
        if (codecInfos == null) return [];

        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var codecInfo in codecInfos)
        {
            if (codecInfo.IsEncoder)
                continue;

            var types = codecInfo.GetSupportedTypes();
            if (types == null) continue;

            foreach (var mime in types)
            {
                if (mimeToCodecMap.TryGetValue(mime, out var k7Codec))
                {
                    supported.Add(k7Codec);
                }
            }
        }

        return [.. supported];
    }
}
