using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Infrastructure.MediaProcessing;

public sealed record VideoEncoderSelection(
    string EncoderName,
    /// <summary>Arguments that must appear before the input (e.g. -init_hw_device).</summary>
    string? GlobalArguments,
    string EncoderArguments,
    /// <summary>Optional -vf chain (without the -vf flag). Applied on the output side.</summary>
    string? VideoFilter,
    bool IsHardwareAccelerated,
    bool UsesHardwareDecode);

public static class FfmpegVideoEncoderBuilder
{
    private static readonly (string LogicalCodec, string SoftwareEncoder, string[] HardwareEncoders)[] CodecMap =
    [
        ("h264", "libx264", ["h264_nvenc", "h264_qsv", "h264_vaapi", "h264_videotoolbox", "h264_amf"]),
        ("hevc", "libx265", ["hevc_nvenc", "hevc_qsv", "hevc_vaapi", "hevc_videotoolbox", "hevc_amf"])
    ];

    public static VideoEncoderSelection? Resolve(
        string logicalCodec,
        TranscodeSettingsDto settings,
        FfmpegCapabilitiesDto capabilities,
        bool forceSoftware = false)
    {
        var map = CodecMap.FirstOrDefault(m =>
            string.Equals(m.LogicalCodec, logicalCodec, StringComparison.OrdinalIgnoreCase));

        if (map.LogicalCodec is null)
            return null;

        if (forceSoftware || settings.EncoderMode == HardwareEncoderMode.Software)
            return CreateSoftware(map);

        if (settings.EncoderMode is HardwareEncoderMode.HardwarePreferred or HardwareEncoderMode.Auto)
        {
            foreach (var hwEncoder in map.HardwareEncoders)
            {
                if (!capabilities.AvailableHardwareEncoders.Contains(hwEncoder, StringComparer.OrdinalIgnoreCase))
                    continue;

                return CreateHardware(hwEncoder);
            }

            if (settings.EncoderMode == HardwareEncoderMode.HardwarePreferred)
                return null;
        }

        return CreateSoftware(map);
    }

    /// <summary>
    /// Builds arguments for a named hardware encoder (used by capability probes).
    /// Returns null when the name is not a known hardware encoder.
    /// </summary>
    public static VideoEncoderSelection? CreateHardwareSelection(string encoderName)
    {
        var known = CodecMap.SelectMany(m => m.HardwareEncoders)
            .Any(e => string.Equals(e, encoderName, StringComparison.OrdinalIgnoreCase));

        return known ? CreateHardware(encoderName) : null;
    }

    /// <summary>
    /// Optional HDR to SDR filter chain. Apply only when the source stream is HDR and tonemap is enabled.
    /// </summary>
    public static string? GetHdrTonemapFilter(bool enableHdrTonemap) =>
        enableHdrTonemap
            ? "zscale=transfer=linear,tonemap=tonemap=hable:desat=0,zscale=transfer=bt709:matrix=bt709:range=tv,format=yuv420p"
            : null;

    public static string? FindVaapiRenderNode()
    {
        const string driPath = "/dev/dri";
        if (!Directory.Exists(driPath))
            return null;

        return Directory.EnumerateFiles(driPath, "renderD*")
            .OrderBy(f => f, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private static VideoEncoderSelection CreateSoftware((string LogicalCodec, string SoftwareEncoder, string[] HardwareEncoders) map)
    {
        var args = map.LogicalCodec switch
        {
            "h264" => "-c:v libx264 -profile:v main -level:v 4.0 -pix_fmt yuv420p",
            "hevc" => "-c:v libx265 -pix_fmt yuv420p",
            _ => $"-c:v {map.SoftwareEncoder}"
        };

        return new VideoEncoderSelection(map.SoftwareEncoder, null, args, null, false, false);
    }

    private static VideoEncoderSelection CreateHardware(string encoder)
    {
        if (encoder.Contains("vaapi", StringComparison.OrdinalIgnoreCase))
            return CreateVaapi(encoder);

        var args = encoder switch
        {
            var e when e.Contains("nvenc", StringComparison.OrdinalIgnoreCase) =>
                $"-c:v {encoder} -preset p4 -pix_fmt yuv420p",
            var e when e.Contains("qsv", StringComparison.OrdinalIgnoreCase) =>
                $"-c:v {encoder} -preset medium -pix_fmt yuv420p",
            var e when e.Contains("videotoolbox", StringComparison.OrdinalIgnoreCase) =>
                $"-c:v {encoder} -profile:v main -pix_fmt yuv420p",
            _ => $"-c:v {encoder} -pix_fmt yuv420p"
        };

        return new VideoEncoderSelection(encoder, null, args, null, true, true);
    }

    private static VideoEncoderSelection CreateVaapi(string encoder)
    {
        // -init_hw_device must appear before -i (see Jellyfin / ffmpeg VAAPI docs).
        var device = FindVaapiRenderNode() ?? "/dev/dri/renderD128";
        var globalArgs = $"-init_hw_device vaapi=va:{device} -filter_hw_device va";
        var encoderArgs = $"-c:v {encoder}";
        const string videoFilter = "format=nv12,hwupload";

        return new VideoEncoderSelection(encoder, globalArgs, encoderArgs, videoFilter, true, false);
    }
}
