using System.Globalization;
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
    bool UsesHardwareDecode,
    /// <summary>Decode flags before -i (e.g. -hwaccel cuda). Not used by capability probes.</summary>
    string? DecodeArguments = null,
    /// <summary>
    /// Hardware scale template with {0}=width and {1}=height. Used instead of CPU scale
    /// so PTS survive GPU decode (scale_cuda / scale_vaapi).
    /// </summary>
    string? HardwareScaleFilterTemplate = null);

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
    /// Software HDR to SDR filter chain (zimg). Prefer applying only when the source is HDR
    /// and Admin tonemap is enabled. Includes linear light + BT.709 primaries to avoid washed output.
    /// </summary>
    public static string? GetHdrTonemapFilter(bool enableHdrTonemap) =>
        enableHdrTonemap
            ? "zscale=transfer=linear:npl=100,format=gbrpf32le,zscale=primaries=bt709,tonemap=tonemap=hable:desat=0,zscale=transfer=bt709:matrix=bt709:range=tv,format=yuv420p"
            : null;

    /// <summary>
    /// Joins optional HDR tonemap, quality scale, and encoder upload filters into one -vf chain.
    /// HDR tonemap is CPU-only: fall back to scale=-2:H then hwupload. Otherwise prefer
    /// the hardware scale template so decode/scale/encode stay on the GPU.
    /// </summary>
    public static string? BuildVideoFilterChain(
        string? hdrTonemapFilter,
        int? scaleHeight,
        string? encoderVideoFilter,
        string? hardwareScaleFilterTemplate = null,
        int? scaleWidth = null)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(hdrTonemapFilter))
            parts.Add(hdrTonemapFilter);

        var useHardwareScale = string.IsNullOrWhiteSpace(hdrTonemapFilter)
            && !string.IsNullOrWhiteSpace(hardwareScaleFilterTemplate)
            && scaleHeight is int;

        if (useHardwareScale && scaleHeight is int hwHeight)
        {
            var width = scaleWidth ?? ClosestEven((int)Math.Round(hwHeight * 16.0 / 9.0));
            parts.Add(string.Format(
                CultureInfo.InvariantCulture,
                hardwareScaleFilterTemplate!,
                width,
                hwHeight));
        }
        else
        {
            if (scaleHeight is int height)
                parts.Add($"scale=-2:{height}");

            if (!string.IsNullOrWhiteSpace(encoderVideoFilter))
                parts.Add(encoderVideoFilter);
        }

        return parts.Count == 0 ? null : string.Join(",", parts);
    }

    private static int ClosestEven(int value) =>
        value < 2 ? 2 : value + (value & 1);

    /// <summary>
    /// Constrains HLS ladder encodes to the advertised quality bitrate (VBV).
    /// Without this, 720p is scale-only and can stay near source bitrate.
    /// </summary>
    public static IReadOnlyList<string> BuildQualityBitrateArguments(int averageBitrate, int maxBitrate)
    {
        // 5x maxrate so VBV can absorb HLS GOP size swings.
        var bufsize = Math.Max(maxBitrate, 1) * 5;
        return
        [
            $"-b:v {averageBitrate}",
            $"-maxrate {maxBitrate}",
            $"-bufsize {bufsize}"
        ];
    }

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
            "h264" => "-c:v libx264 -preset veryfast -profile:v main -level:v 4.0 -pix_fmt yuv420p -sc_threshold 0",
            "hevc" => "-c:v libx265 -pix_fmt yuv420p",
            _ => $"-c:v {map.SoftwareEncoder}"
        };

        return new VideoEncoderSelection(map.SoftwareEncoder, null, args, null, false, false);
    }

    private static VideoEncoderSelection CreateHardware(string encoder)
    {
        if (encoder.Contains("vaapi", StringComparison.OrdinalIgnoreCase))
            return CreateVaapi(encoder);

        if (encoder.Contains("nvenc", StringComparison.OrdinalIgnoreCase))
            return CreateNvenc(encoder);

        if (encoder.Contains("amf", StringComparison.OrdinalIgnoreCase))
            return CreateAmf(encoder);

        var args = encoder switch
        {
            var e when e.Contains("qsv", StringComparison.OrdinalIgnoreCase) =>
                $"-c:v {encoder} -preset medium -pix_fmt yuv420p",
            var e when e.Contains("videotoolbox", StringComparison.OrdinalIgnoreCase) =>
                $"-c:v {encoder} -profile:v main -pix_fmt yuv420p",
            _ => $"-c:v {encoder} -pix_fmt yuv420p"
        };

        return new VideoEncoderSelection(encoder, null, args, null, true, true);
    }

    private static VideoEncoderSelection CreateAmf(string encoder)
    {
        // Do not pair Auto/DXVA2 decode with AMF. ffmpeg tries to derive an AMF context
        // from the D3D9 device and often fails with "No such device" (probe still passes
        // because lavfi verification has no DXVA2 decode). System frames + AMF encode.
        var args = $"-c:v {encoder} -quality balanced -rc cbr -pix_fmt yuv420p -forced_idr 1";
        return new VideoEncoderSelection(
            encoder,
            GlobalArguments: null,
            EncoderArguments: args,
            VideoFilter: null,
            IsHardwareAccelerated: true,
            UsesHardwareDecode: false);
    }

    private static VideoEncoderSelection CreateNvenc(string encoder)
    {
        // Stay on GPU for the whole dec/scale/enc path. CPU scale=-2:H between
        // Auto hwaccel and NVENC resets PTS so force_key_frames never fire.
        var args =
            $"-c:v {encoder} -preset p4 -rc vbr -no-scenecut 1 -zerolatency 1 -rc-lookahead 0 -pix_fmt yuv420p";
        return new VideoEncoderSelection(
            encoder,
            GlobalArguments: null,
            EncoderArguments: args,
            VideoFilter: "format=nv12|cuda,hwupload,scale_cuda=format=nv12",
            IsHardwareAccelerated: true,
            UsesHardwareDecode: true,
            DecodeArguments: "-hwaccel cuda -hwaccel_output_format cuda",
            HardwareScaleFilterTemplate: "format=nv12|cuda,hwupload,scale_cuda={0}:{1}:format=nv12");
    }

    private static VideoEncoderSelection CreateVaapi(string encoder)
    {
        // ffmpeg requires -init_hw_device before -i.
        var device = FindVaapiRenderNode() ?? "/dev/dri/renderD128";
        var globalArgs = $"-init_hw_device vaapi=va:{device} -filter_hw_device va";
        var encoderArgs = $"-c:v {encoder}";
        const string videoFilter = "format=nv12,hwupload";

        return new VideoEncoderSelection(encoder, globalArgs, encoderArgs, videoFilter, true, false);
    }
}
