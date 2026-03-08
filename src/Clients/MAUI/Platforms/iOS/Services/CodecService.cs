using K7.Clients.MAUI.Interfaces;
using UIKit;

namespace K7.Clients.MAUI.Platforms.iOS.Services;

public class CodecService : ICodecService
{
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
        ["flv"] = ["h264", "aac"],
    };

    public Task<bool> GetHdrSupportAsync()
    {
        var screen = UIScreen.MainScreen;
        return Task.FromResult(screen.TraitCollection.DisplayGamut == UIDisplayGamut.P3);
    }

    public Task<string[]> GetSupportedVideoCodecsAsync()
    {
        var supported = new List<string> { "h264", "mpeg4" };

        // HEVC hardware decoding available since iOS 11 (A9+ chips)
        if (UIDevice.CurrentDevice.CheckSystemVersion(11, 0))
            supported.Add("hevc");

        // VP9 software decoding available since iOS 14
        if (UIDevice.CurrentDevice.CheckSystemVersion(14, 0))
            supported.Add("vp9");

        return Task.FromResult(supported.ToArray());
    }

    public Task<string[]> GetSupportedAudioCodecsAsync()
    {
        var supported = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "aac", "mp3", "flac", "alac", "ac3", "eac3", "pcm"
        };

        if (UIDevice.CurrentDevice.CheckSystemVersion(11, 0))
            supported.Add("opus");

        return Task.FromResult(supported.ToArray());
    }

    public Task<string[]> GetSupportedContainersAsync()
    {
        var allCodecs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        allCodecs.UnionWith(GetSupportedVideoCodecsAsync().Result);
        allCodecs.UnionWith(GetSupportedAudioCodecsAsync().Result);

        var containers = ContainerToRequiredCodecs
            .Where(kv => kv.Value.Any(c => allCodecs.Contains(c)))
            .Select(kv => kv.Key)
            .ToArray();

        return Task.FromResult(containers);
    }
}
