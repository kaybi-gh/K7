using System.Text.RegularExpressions;
using FFMpegCore;
using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing;

public partial class FfmpegCapabilitiesService(
    ITranscodeSettingsProvider transcodeSettingsProvider,
    ILogger<FfmpegCapabilitiesService> logger) : IFfmpegCapabilitiesService
{
    private static readonly string[] PreferredHardwareEncoders =
    [
        "h264_nvenc", "hevc_nvenc",
        "h264_qsv", "hevc_qsv",
        "h264_vaapi", "hevc_vaapi",
        "h264_videotoolbox", "hevc_videotoolbox",
        "h264_amf", "hevc_amf"
    ];

    private FfmpegCapabilitiesDto? _cachedCapabilities;
    private readonly SemaphoreSlim _probeLock = new(1, 1);

    public async Task<FfmpegCapabilitiesDto> GetCapabilitiesAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedCapabilities is not null)
            return _cachedCapabilities;

        await _probeLock.WaitAsync(cancellationToken);
        try
        {
            _cachedCapabilities ??= await ProbeCapabilitiesAsync(cancellationToken);
            return _cachedCapabilities;
        }
        finally
        {
            _probeLock.Release();
        }
    }

    public async Task<VideoEncoderInfoDto?> ResolveVideoEncoderAsync(
        string logicalCodec,
        bool forceSoftware = false,
        CancellationToken cancellationToken = default)
    {
        var capabilities = await GetCapabilitiesAsync(cancellationToken);
        var settings = await transcodeSettingsProvider.GetSettingsAsync(cancellationToken);
        var selection = FfmpegVideoEncoderBuilder.Resolve(logicalCodec, settings, capabilities, forceSoftware);
        return selection is null
            ? null
            : new VideoEncoderInfoDto
            {
                EncoderName = selection.EncoderName,
                IsHardwareAccelerated = selection.IsHardwareAccelerated
            };
    }

    private static async Task<(int ExitCode, string Stdout, string Stderr)> RunCaptureAsync(
        string fileName,
        string arguments,
        CancellationToken cancellationToken)
    {
        var stdout = new List<string>();
        var stderr = new List<string>();
        var exitCode = await SafeProcessRunner.RunAsync(
            fileName,
            arguments,
            line => stdout.Add(line),
            line => stderr.Add(line),
            cancellationToken: cancellationToken);

        return (exitCode, string.Join('\n', stdout), string.Join('\n', stderr));
    }

    private const int TestFrameWidth = 320;
    private const int TestFrameHeight = 240;

    public async Task<FfmpegTranscodeTestResultDto> TestEncoderAsync(CancellationToken cancellationToken = default)
    {
        var capabilities = await GetCapabilitiesAsync(cancellationToken);
        var settings = await transcodeSettingsProvider.GetSettingsAsync(cancellationToken);
        var selection = FfmpegVideoEncoderBuilder.Resolve("h264", settings, capabilities);

        if (selection is null)
        {
            return new FfmpegTranscodeTestResultDto
            {
                Success = false,
                Error = "No suitable H.264 encoder found.",
                Capabilities = capabilities
            };
        }

        var ffmpegPath = GlobalFFOptions.GetFFMpegBinaryPath();
        var args = $"-hide_banner -f lavfi -i color=c=black:s={TestFrameWidth}x{TestFrameHeight}:d=0.1 {selection.EncoderArguments} -f null -";

        try
        {
            var result = await RunCaptureAsync(ffmpegPath, args, cancellationToken);
            if (result.ExitCode != 0)
            {
                return new FfmpegTranscodeTestResultDto
                {
                    Success = false,
                    SelectedEncoder = selection.EncoderName,
                    IsHardwareAccelerated = selection.IsHardwareAccelerated,
                    Error = result.Stderr,
                    Capabilities = capabilities
                };
            }

            return new FfmpegTranscodeTestResultDto
            {
                Success = true,
                SelectedEncoder = selection.EncoderName,
                IsHardwareAccelerated = selection.IsHardwareAccelerated,
                Capabilities = capabilities
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FFmpeg encoder test failed");
            return new FfmpegTranscodeTestResultDto
            {
                Success = false,
                SelectedEncoder = selection.EncoderName,
                IsHardwareAccelerated = selection.IsHardwareAccelerated,
                Error = ex.Message,
                Capabilities = capabilities
            };
        }
    }

    private static async Task<FfmpegCapabilitiesDto> ProbeCapabilitiesAsync(CancellationToken cancellationToken)
    {
        var ffmpegPath = GlobalFFOptions.GetFFMpegBinaryPath();
        var versionResult = await RunCaptureAsync(ffmpegPath, "-hide_banner -version", cancellationToken);
        var versionLine = versionResult.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim();

        var hwaccelResult = await RunCaptureAsync(ffmpegPath, "-hide_banner -hwaccels", cancellationToken);
        var hwaccels = ParseLines(hwaccelResult.Stdout, skipHeader: true);

        var encodersResult = await RunCaptureAsync(ffmpegPath, "-hide_banner -encoders", cancellationToken);
        var encoders = ParseEncoderNames(encodersResult.Stdout);
        var hardwareEncoders = encoders
            .Where(e => PreferredHardwareEncoders.Contains(e, StringComparer.OrdinalIgnoreCase))
            .ToList();

        return new FfmpegCapabilitiesDto
        {
            FfmpegVersion = versionLine,
            HardwareAccelerators = hwaccels,
            VideoEncoders = encoders,
            AvailableHardwareEncoders = hardwareEncoders
        };
    }

    private static List<string> ParseLines(string output, bool skipHeader)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (skipHeader && lines.Length > 0)
            lines = [.. lines.Skip(1)];

        return [.. lines.Where(l => !string.IsNullOrWhiteSpace(l))];
    }

    private static List<string> ParseEncoderNames(string output)
    {
        var encoders = new List<string>();
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var match = EncoderLineRegex().Match(line);
            if (match.Success)
                encoders.Add(match.Groups[1].Value);
        }

        return encoders;
    }

    [GeneratedRegex(@"^\s*[AVSFDK][\w\.]+\s+([\w\-]+)\s+", RegexOptions.CultureInvariant)]
    private static partial Regex EncoderLineRegex();
}
