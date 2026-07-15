using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Infrastructure.MediaProcessing;

public sealed record VideoEncoderSelection(
    string EncoderName,
    string EncoderArguments,
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

                return CreateHardware(map.LogicalCodec, hwEncoder, settings.EnableHdrTonemap);
            }

            if (settings.EncoderMode == HardwareEncoderMode.HardwarePreferred)
                return null;
        }

        return CreateSoftware(map);
    }

    private static VideoEncoderSelection CreateSoftware((string LogicalCodec, string SoftwareEncoder, string[] HardwareEncoders) map)
    {
        var args = map.LogicalCodec switch
        {
            "h264" => "-c:v libx264 -profile:v main -level:v 4.0 -pix_fmt yuv420p",
            "hevc" => "-c:v libx265 -pix_fmt yuv420p",
            _ => $"-c:v {map.SoftwareEncoder}"
        };

        return new VideoEncoderSelection(map.SoftwareEncoder, args, false, false);
    }

    private static VideoEncoderSelection CreateHardware(string logicalCodec, string encoder, bool enableHdrTonemap)
    {
        var tonemap = enableHdrTonemap
            ? "-vf \"zscale=transfer=linear,tonemap=tonemap=hable:desat=0,zscale=transfer=bt709:matrix=bt709:range=tv,format=yuv420p\""
            : "-pix_fmt yuv420p";

        var args = encoder switch
        {
            var e when e.Contains("nvenc", StringComparison.OrdinalIgnoreCase) =>
                $"-c:v {encoder} -preset p4 {tonemap}",
            var e when e.Contains("qsv", StringComparison.OrdinalIgnoreCase) =>
                $"-c:v {encoder} -preset medium {tonemap}",
            var e when e.Contains("vaapi", StringComparison.OrdinalIgnoreCase) =>
                $"-vf \"format=nv12,hwupload\" -c:v {encoder} {tonemap}",
            var e when e.Contains("videotoolbox", StringComparison.OrdinalIgnoreCase) =>
                $"-c:v {encoder} -profile:v main {tonemap}",
            _ => $"-c:v {encoder} {tonemap}"
        };

        return new VideoEncoderSelection(encoder, args, true, true);
    }
}
