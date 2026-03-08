using K7.Clients.MAUI.Interfaces;
using Microsoft.UI.Dispatching;
using Windows.Graphics.Display;
using Windows.Media.Core;

namespace K7.Clients.MAUI.Platforms.Windows.Services;

public class CodecService : ICodecService
{
    // GUIDs for codecs not available as CodecSubtypes constants on SDK 19041
    private const string VideoFormatVP9 = "{30395056-0000-0010-8000-00AA00389B71}";
    private const string VideoFormatAV1 = "{31305641-0000-0010-8000-00AA00389B71}";
    private const string AudioFormatEAC3 = "{33434145-0000-0010-8000-00AA00389B71}";

    private static readonly Dictionary<string, string> VideoSubtypeToCodec = new(StringComparer.OrdinalIgnoreCase)
    {
        [CodecSubtypes.VideoFormatH264] = "h264",
        [CodecSubtypes.VideoFormatHevc] = "hevc",
        [CodecSubtypes.VideoFormatVP80] = "vp8",
        [VideoFormatVP9] = "vp9",
        [VideoFormatAV1] = "av1",
        [CodecSubtypes.VideoFormatMpeg2] = "mpeg2",
        [CodecSubtypes.VideoFormatMP4V] = "mpeg4",
    };

    private static readonly Dictionary<string, string> AudioSubtypeToCodec = new(StringComparer.OrdinalIgnoreCase)
    {
        [CodecSubtypes.AudioFormatAac] = "aac",
        [CodecSubtypes.AudioFormatMP3] = "mp3",
        [CodecSubtypes.AudioFormatFlac] = "flac",
        [CodecSubtypes.AudioFormatOpus] = "opus",
        [CodecSubtypes.AudioFormatDolbyAC3] = "ac3",
        [AudioFormatEAC3] = "eac3",
        [CodecSubtypes.AudioFormatPcm] = "pcm",
        [CodecSubtypes.AudioFormatAlac] = "alac",
    };

    // Container support derived from codec availability
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

    public async Task<bool> GetHdrSupportAsync()
    {
        bool supportsHdr = false;

        var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
        if (dispatcherQueue != null)
        {
            var taskCompletionSource = new TaskCompletionSource<bool>();

            dispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    var displayInfo = DisplayInformation.GetForCurrentView();
                    supportsHdr = displayInfo.GetAdvancedColorInfo().CurrentAdvancedColorKind != AdvancedColorKind.StandardDynamicRange;
                }
                catch
                {
                    supportsHdr = false;
                }
                taskCompletionSource.SetResult(supportsHdr);
            });

            return await taskCompletionSource.Task;
        }

        return false;
    }

    public async Task<string[]> GetSupportedVideoCodecsAsync()
    {
        var codecQuery = new CodecQuery();
        var query = await codecQuery.FindAllAsync(CodecKind.Video, CodecCategory.Decoder, "");
        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var codec in query)
        {
            foreach (var subtype in codec.Subtypes)
            {
                if (VideoSubtypeToCodec.TryGetValue(subtype, out var k7Codec))
                {
                    supported.Add(k7Codec);
                }
            }
        }

        return [.. supported];
    }

    public async Task<string[]> GetSupportedAudioCodecsAsync()
    {
        var codecQuery = new CodecQuery();
        var query = await codecQuery.FindAllAsync(CodecKind.Audio, CodecCategory.Decoder, "");
        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var codec in query)
        {
            foreach (var subtype in codec.Subtypes)
            {
                if (AudioSubtypeToCodec.TryGetValue(subtype, out var k7Codec))
                {
                    supported.Add(k7Codec);
                }
            }
        }

        return [.. supported];
    }

    public async Task<string[]> GetSupportedContainersAsync()
    {
        var audioCodecs = new HashSet<string>(await GetSupportedAudioCodecsAsync(), StringComparer.OrdinalIgnoreCase);
        var videoCodecs = new HashSet<string>(await GetSupportedVideoCodecsAsync(), StringComparer.OrdinalIgnoreCase);
        var allCodecs = new HashSet<string>(audioCodecs, StringComparer.OrdinalIgnoreCase);
        allCodecs.UnionWith(videoCodecs);

        var containers = ContainerToRequiredCodecs
            .Where(kv => kv.Value.Any(c => allCodecs.Contains(c)))
            .Select(kv => kv.Key)
            .ToArray();

        return containers;
    }
}
