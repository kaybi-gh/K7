using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing;

public class MediaTranscoder : IMediaTranscoder
{
    private readonly ILogger<MediaTranscoder> _logger;
    private readonly IFfmpegCapabilitiesService _ffmpegCapabilitiesService;
    private readonly ITranscodeSettingsProvider _transcodeSettingsProvider;

    public MediaTranscoder(
        ILogger<MediaTranscoder> logger,
        IFfmpegCapabilitiesService ffmpegCapabilitiesService,
        ITranscodeSettingsProvider transcodeSettingsProvider)
    {
        _logger = logger;
        _ffmpegCapabilitiesService = ffmpegCapabilitiesService;
        _transcodeSettingsProvider = transcodeSettingsProvider;
    }
    public async Task RemuxVideoAsync(
        string inputFilePath,
        string tempDirectory,
        string outputFilePath,
        List<HlsSegment> segments,
        CancellationToken cancellationToken = default)
    {
        var firstSegment = segments.First();
        var lastSegment = segments.Last();

        var outputDirectory = new FileInfo(outputFilePath).DirectoryName!;

        var segmentDuration = TimeSpan.FromMilliseconds(firstSegment.Duration);

        await FFMpegArguments
            .FromFileInput(inputFilePath, verifyExists: true, options => options
                .WithHardwareAcceleration(HardwareAccelerationDevice.Auto)
                .ConfigureSeekOptions(firstSegment, lastSegment))
            .OutputToFile(outputFilePath, overwrite: true, options => options
                .SelectStream(0, channel: Channel.Video)
                .SelectStream(0, channel: Channel.Audio)
                .CopyChannel(Channel.All)
                .WithCustomArgument("-f hls")
                .WithCustomArgument("-hls_segment_type fmp4")
                .WithCustomArgument("-hls_flags independent_segments")
                .WithCustomArgument("-hls_fmp4_init_filename init.m4s")
                .WithCustomArgument("-hls_segment_filename segment_%05d.m4s"))
            .Configure(options => options.WorkingDirectory = outputDirectory)
            .Configure(options => options.TemporaryFilesFolder = tempDirectory)
            .CancellableThrough(cancellationToken, timeout: (int)TimeSpan.FromMinutes(30).TotalMilliseconds)
            .ProcessAsynchronously(throwOnError: true)
            .ConfigureAwait(false);

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    public async Task TranscodeVideoAsync(
        string inputFilePath,
        string tempDirectory,
        string outputFilePath,
        List<HlsSegment> segments,
        string videoResolutionIdentifier,
        CancellationToken cancellationToken = default)
    {
        var quality = Constants.VideoQualities.FirstOrDefault(kvp => kvp.Value.Name == videoResolutionIdentifier).Value;
        var firstSegment = segments.First();
        var lastSegment = segments.Last();

        var outputDirectory = new FileInfo(outputFilePath).DirectoryName!;

        var segmentDuration = TimeSpan.FromMilliseconds(firstSegment.Duration);

        await FFMpegArguments
            .FromFileInput(inputFilePath, verifyExists: true, options => options
                .WithHardwareAcceleration(HardwareAccelerationDevice.Auto)
                .ConfigureSeekOptions(firstSegment, lastSegment))
            .OutputToFile(outputFilePath, overwrite: true, options => options
                .SelectStream(0, channel: Channel.Video)
                .WithVideoCodec(VideoCodec.LibX264)
                .SelectStream(0, channel: Channel.Audio)
                .WithAudioCodec(AudioCodec.Aac)
                .ConfigureVideoScalingHlsOptions(quality?.Height)
                .WithCustomArgument("-an")
                .WithCustomArgument($"-t {segmentDuration.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}")
                .WithCustomArgument("-f hls")
                .WithCustomArgument("-hls_segment_type fmp4")
                .WithCustomArgument("-hls_flags independent_segments")
                .WithCustomArgument("-hls_fmp4_init_filename init.m4s")
                .WithCustomArgument("-hls_segment_filename segment_%05d.m4s"))
            .Configure(options => options.WorkingDirectory = outputDirectory)
            .Configure(options => options.TemporaryFilesFolder = tempDirectory)
            .CancellableThrough(cancellationToken, timeout: (int)TimeSpan.FromMinutes(30).TotalMilliseconds)
            .ProcessAsynchronously(throwOnError: true)
            .ConfigureAwait(false);

        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    public async Task StartVideoStreamingTranscodeAsync(
        string inputFilePath,
        string outputDirectory,
        List<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        CancellationToken cancellationToken,
        string? videoCodec = null,
        string? videoResolutionIdentifier = null,
        int? subtitleBurnInStreamIndex = null)
    {
        var (startTime, endTime) = ValidateAndComputeTimeRange(allSegments, startSegmentIndex, endSegmentIndex);
        Directory.CreateDirectory(outputDirectory);

        var hasBurnIn = subtitleBurnInStreamIndex.HasValue;
        // Burn-in forces a transcode (can't stream copy when overlaying subtitles)
        var needsTranscode = !string.IsNullOrEmpty(videoCodec) || hasBurnIn;

        _logger.LogInformation(
            "Starting video streaming transcode: input={Input}, output={Output}, startSeg={Start}, endSeg={End}, videoCodec={VideoCodec}, burnInStreamIndex={BurnInStreamIndex}",
            inputFilePath, outputDirectory, startSegmentIndex, endSegmentIndex, videoCodec ?? "copy", subtitleBurnInStreamIndex?.ToString() ?? "none");

        var stderrLines = new List<string>();
        var burnInFilterComplex = string.Empty;
        string? burnInCanvasSizeArg = null;

        if (hasBurnIn)
        {
            var dimensions = await GetBurnInStreamDimensionsAsync(
                inputFilePath,
                subtitleBurnInStreamIndex!.Value,
                cancellationToken);
            burnInFilterComplex = PgsBurnInFilterBuilder.BuildFilterComplex(
                subtitleBurnInStreamIndex.Value,
                dimensions.VideoWidth,
                dimensions.VideoHeight,
                dimensions.SubtitleWidth,
                dimensions.SubtitleHeight);
            burnInCanvasSizeArg = PgsBurnInFilterBuilder.BuildCanvasSizeArgument(
                dimensions.SubtitleWidth,
                dimensions.SubtitleHeight);

            _logger.LogInformation(
                "PGS burn-in filter for stream {SubStreamIndex}: video={VideoWidth}x{VideoHeight}, subtitle={SubtitleWidth}x{SubtitleHeight}, canvasSize={CanvasSize}",
                subtitleBurnInStreamIndex.Value,
                dimensions.VideoWidth,
                dimensions.VideoHeight,
                dimensions.SubtitleWidth,
                dimensions.SubtitleHeight,
                burnInCanvasSizeArg ?? "default");
        }

        VideoEncoderSelection? encoderSelection = null;
        if (needsTranscode)
        {
            var effectiveCodec = videoCodec ?? (hasBurnIn ? "h264" : null);
            if (effectiveCodec is not null)
            {
                var settings = await _transcodeSettingsProvider.GetSettingsAsync(cancellationToken);
                var capabilities = await _ffmpegCapabilitiesService.GetCapabilitiesAsync(cancellationToken);
                encoderSelection = FfmpegVideoEncoderBuilder.Resolve(effectiveCodec, settings, capabilities, hasBurnIn);
            }
        }

        var resolvedEncoder = encoderSelection;

        var scaleHeight = ResolveScaleHeight(videoResolutionIdentifier);

        var ffmpegTask = FFMpegArguments
            .FromFileInput(inputFilePath, verifyExists: true, options =>
            {
                if (hasBurnIn)
                {
                    options.WithCustomArgument("-probesize 50000000");
                    options.WithCustomArgument("-analyzeduration 50000000");
                    if (burnInCanvasSizeArg is not null)
                    {
                        options.WithCustomArgument(burnInCanvasSizeArg);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(resolvedEncoder?.GlobalArguments))
                {
                    // VAAPI (and similar): -init_hw_device must come before -i.
                    options.WithCustomArgument(resolvedEncoder.GlobalArguments);
                }
                else
                {
                    options.WithHardwareAcceleration(HardwareAccelerationDevice.Auto);
                }

                options.Seek(startTime);
            })
            .OutputToFile("index.m3u8", overwrite: true, options =>
            {
                if (hasBurnIn)
                {
                    options.WithCustomArgument($"-filter_complex \"{burnInFilterComplex}\"");
                    options.WithCustomArgument("-map \"[vout]\"");
                    options.WithCustomArgument("-an");
                }
                else
                {
                    options.WithCustomArgument("-map 0:v:0");
                    options.WithCustomArgument("-an");
                }

                if (needsTranscode)
                {
                    var effectiveCodec = videoCodec ?? (hasBurnIn ? "h264" : null);
                    if (effectiveCodec is not null)
                    {
                        if (resolvedEncoder is null)
                        {
                            _logger.LogWarning(
                                "No encoder resolved for codec {Codec}, falling back to libx264",
                                effectiveCodec);
                            options.WithCustomArgument("-c:v libx264")
                                .WithCustomArgument("-profile:v main")
                                .WithCustomArgument("-level:v 4.0")
                                .WithCustomArgument("-pix_fmt yuv420p");
                        }
                        else
                        {
                            _logger.LogInformation(
                                "Using video encoder {Encoder} (hardware={IsHardware}) for codec {Codec}",
                                resolvedEncoder.EncoderName,
                                resolvedEncoder.IsHardwareAccelerated,
                                effectiveCodec);
                            options.WithCustomArgument(resolvedEncoder.EncoderArguments);
                        }
                    }

                    ApplyVideoFilterOrScale(options, resolvedEncoder, scaleHeight);
                }
                else
                {
                    options.WithCustomArgument("-c:v copy");
                    options.WithCustomArgument("-tag:v hvc1");
                }

                ConfigureHlsOutput(options, startSegmentIndex, endTime);
            })
            .Configure(options => options.WorkingDirectory = outputDirectory)
            .Configure(options => options.TemporaryFilesFolder = outputDirectory)
            .NotifyOnOutput((output) => _logger.LogDebug("FFmpeg stdout: {Output}", output))
            .NotifyOnError((error) =>
            {
                stderrLines.Add(error);
                _logger.LogDebug("FFmpeg stderr: {Error}", error);
            })
            .CancellableThrough(cancellationToken);

        var result = await ffmpegTask.ProcessAsynchronously(throwOnError: false);
        if (!result)
        {
            var stderr = string.Join(Environment.NewLine, stderrLines);
            _logger.LogError(
                "FFmpeg video transcode failed for {Input}, burnInStreamIndex={BurnInStreamIndex}. Stderr: {Stderr}",
                inputFilePath,
                subtitleBurnInStreamIndex?.ToString() ?? "none",
                stderr);
        }

        _logger.LogInformation("FFmpeg video process completed for output directory: {OutputDir}, Success: {Success}", outputDirectory, result);
    }

    private static async Task<(int VideoWidth, int VideoHeight, int SubtitleWidth, int SubtitleHeight)> GetBurnInStreamDimensionsAsync(
        string inputFilePath,
        int subtitleStreamIndex,
        CancellationToken cancellationToken)
    {
        var mediaAnalysis = await FFProbe.AnalyseAsync(inputFilePath, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var videoStream = mediaAnalysis.PrimaryVideoStream;
        var subtitleDimensions = await ProbeStreamDimensionsAsync(
            inputFilePath,
            subtitleStreamIndex,
            cancellationToken);

        return (
            videoStream?.Width ?? 0,
            videoStream?.Height ?? 0,
            subtitleDimensions.Width,
            subtitleDimensions.Height);
    }

    private static async Task<(int Width, int Height)> ProbeStreamDimensionsAsync(
        string inputFilePath,
        int streamIndex,
        CancellationToken cancellationToken)
    {
        var stdoutLines = new List<string>();
        var ffprobePath = GlobalFFOptions.GetFFProbeBinaryPath();
        var arguments =
            $"-v error -select_streams {streamIndex} -show_entries stream=width,height,coded_width,coded_height -of json \"{inputFilePath}\"";

        var exitCode = await SafeProcessRunner.RunAsync(
            ffprobePath,
            arguments,
            onStdout: line => stdoutLines.Add(line),
            timeout: TimeSpan.FromSeconds(30),
            cancellationToken: cancellationToken);

        if (exitCode != 0 || stdoutLines.Count == 0)
        {
            return (0, 0);
        }

        try
        {
            using var doc = JsonDocument.Parse(string.Join(string.Empty, stdoutLines));
            if (!doc.RootElement.TryGetProperty("streams", out var streams)
                || streams.GetArrayLength() == 0)
            {
                return (0, 0);
            }

            var stream = streams[0];
            var width = ReadStreamDimension(stream, "width", "coded_width");
            var height = ReadStreamDimension(stream, "height", "coded_height");
            return (width, height);
        }
        catch (JsonException)
        {
            return (0, 0);
        }
    }

    private static int ReadStreamDimension(JsonElement stream, string primaryKey, string fallbackKey)
    {
        if (stream.TryGetProperty(primaryKey, out var primary)
            && primary.TryGetInt32(out var primaryValue)
            && primaryValue > 0)
        {
            return primaryValue;
        }

        if (stream.TryGetProperty(fallbackKey, out var fallback)
            && fallback.TryGetInt32(out var fallbackValue)
            && fallbackValue > 0)
        {
            return fallbackValue;
        }

        return 0;
    }

    public async Task StartAudioStreamingTranscodeAsync(
        string inputFilePath,
        string outputDirectory,
        List<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        CancellationToken cancellationToken,
        int audioTrackIndex,
        string? audioCodec = null)
    {
        var (startTime, endTime) = ValidateAndComputeTimeRange(allSegments, startSegmentIndex, endSegmentIndex);
        Directory.CreateDirectory(outputDirectory);

        var needsTranscode = !string.IsNullOrEmpty(audioCodec);

        _logger.LogInformation(
            "Starting audio streaming transcode: input={Input}, output={Output}, startSeg={Start}, endSeg={End}, audioCodec={AudioCodec}, track={Track}",
            inputFilePath, outputDirectory, startSegmentIndex, endSegmentIndex, audioCodec ?? "copy", audioTrackIndex);

        var ffmpegTask = FFMpegArguments
            .FromFileInput(inputFilePath, verifyExists: true, options => options
                .WithHardwareAcceleration(HardwareAccelerationDevice.Auto)
                .Seek(startTime))
            .OutputToFile("index.m3u8", overwrite: true, options =>
            {
                options.WithCustomArgument($"-map 0:{audioTrackIndex}");
                options.WithCustomArgument("-vn");

                if (needsTranscode)
                {
                    if (audioCodec == "aac")
                    {
                        options.WithCustomArgument("-c:a aac")
                            .WithCustomArgument("-ac 2")
                            .WithCustomArgument("-ar 48000")
                            .WithCustomArgument("-b:a 128k");
                    }
                    else if (audioCodec == "opus")
                    {
                        options.WithCustomArgument("-c:a libopus");
                    }
                }
                else
                {
                    options.WithCustomArgument("-c:a copy");
                }

                ConfigureHlsOutput(options, startSegmentIndex, endTime);
            })
            .Configure(options => options.WorkingDirectory = outputDirectory)
            .Configure(options => options.TemporaryFilesFolder = outputDirectory)
            .NotifyOnOutput((output) => _logger.LogDebug("FFmpeg stdout: {Output}", output))
            .NotifyOnError((error) => _logger.LogDebug("FFmpeg stderr: {Error}", error))
            .CancellableThrough(cancellationToken);

        var result = await ffmpegTask.ProcessAsynchronously(throwOnError: false);
        _logger.LogInformation("FFmpeg audio process completed for output directory: {OutputDir}, Success: {Success}", outputDirectory, result);
    }

    private static (TimeSpan StartTime, TimeSpan EndTime) ValidateAndComputeTimeRange(
        List<HlsSegment> allSegments, int startSegmentIndex, int endSegmentIndex)
    {
        if (startSegmentIndex < 0 || endSegmentIndex > allSegments.Count || startSegmentIndex >= endSegmentIndex)
        {
            throw new ArgumentException("Invalid segment range");
        }

        var firstSegment = allSegments[startSegmentIndex];
        var lastSegment = allSegments[endSegmentIndex - 1];

        var startTime = TimeSpan.FromMilliseconds(firstSegment.StartTimestamp);
        var endTime = TimeSpan.FromMilliseconds(lastSegment.StartTimestamp + lastSegment.Duration);

        return (startTime, endTime);
    }

    private static void ConfigureHlsOutput(FFMpegArgumentOptions options, int startSegmentIndex, TimeSpan endTime)
    {
        // Duration limit (using -to because of -copyts)
        options.WithCustomArgument($"-to {endTime.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");

        // Use absolute timestamps to align with the initial init.m4s
        options.WithCustomArgument("-copyts")
            .WithCustomArgument("-avoid_negative_ts disabled");

        // HLS output format with fMP4
        options.WithCustomArgument("-f hls")
            .WithCustomArgument("-hls_time 6")
            .WithCustomArgument("-hls_segment_type fmp4")
            .WithCustomArgument("-hls_segment_options movflags=+frag_discont")
            .WithCustomArgument("-hls_flags independent_segments")
            .WithCustomArgument($"-start_number {startSegmentIndex}")
            .WithCustomArgument("-hls_fmp4_init_filename init.m4s")
            .WithCustomArgument("-hls_segment_filename %d.m4s")
            .WithCustomArgument("-hls_playlist_type vod")
            .WithCustomArgument("-hls_list_size 0");
    }

    public async Task ExtractSubtitleAsVttAsync(
        string inputFilePath,
        int subtitleStreamIndex,
        string outputVttPath,
        CancellationToken cancellationToken = default)
    {
        var outputDir = Path.GetDirectoryName(outputVttPath)!;
        Directory.CreateDirectory(outputDir);

        _logger.LogInformation(
            "Extracting subtitle stream {StreamIndex} from {Input} to {Output}",
            subtitleStreamIndex, inputFilePath, outputVttPath);

        var stderrLines = new List<string>();

        var result = await FFMpegArguments
            .FromFileInput(inputFilePath, verifyExists: true)
            .OutputToFile(outputVttPath, overwrite: true, options =>
            {
                options.WithCustomArgument($"-map 0:{subtitleStreamIndex}");
                options.WithCustomArgument("-c:s webvtt");
            })
            .NotifyOnOutput((output) => _logger.LogDebug("FFmpeg subtitle stdout: {Output}", output))
            .NotifyOnError((error) => stderrLines.Add(error))
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously(throwOnError: false);

        if (!result || !File.Exists(outputVttPath))
        {
            var stderr = string.Join(Environment.NewLine, stderrLines);
            _logger.LogError(
                "Failed to extract subtitle stream {StreamIndex} from {Input}. FFmpeg stderr: {Stderr}",
                subtitleStreamIndex, inputFilePath, stderr);
            throw new InvalidOperationException($"FFmpeg failed to extract subtitle stream {subtitleStreamIndex}");
        }

        _logger.LogInformation("Subtitle extraction completed: {Output}", outputVttPath);
    }

    public async Task RemuxWithAudioTranscodeAsync(
        string inputFilePath,
        string outputFilePath,
        int audioTrackIndex,
        int[]? subtitleTrackIndices = null,
        CancellationToken cancellationToken = default)
    {
        var outputDir = Path.GetDirectoryName(outputFilePath)!;
        Directory.CreateDirectory(outputDir);

        _logger.LogInformation(
            "Remuxing {Input} -> {Output} with audio track {AudioTrack} transcoded to AAC",
            inputFilePath, outputFilePath, audioTrackIndex);

        var result = await FFMpegArguments
            .FromFileInput(inputFilePath, verifyExists: true)
            .OutputToFile(outputFilePath, overwrite: true, options =>
            {
                options.WithCustomArgument("-map 0:v:0");
                options.WithCustomArgument($"-map 0:a:{audioTrackIndex}");

                if (subtitleTrackIndices is { Length: > 0 })
                {
                    foreach (var subIndex in subtitleTrackIndices)
                    {
                        options.WithCustomArgument($"-map 0:s:{subIndex}");
                    }
                    options.WithCustomArgument("-c:s mov_text");
                }

                options.WithCustomArgument("-c:v copy");
                options.WithCustomArgument("-c:a aac");
                options.WithCustomArgument("-b:a 192k");
                options.WithCustomArgument("-movflags +faststart");
            })
            .NotifyOnOutput((output) => _logger.LogDebug("FFmpeg remux stdout: {Output}", output))
            .NotifyOnError((error) => _logger.LogDebug("FFmpeg remux stderr: {Error}", error))
            .CancellableThrough(cancellationToken, timeout: (int)TimeSpan.FromHours(2).TotalMilliseconds)
            .ProcessAsynchronously(throwOnError: false);

        if (!result || !File.Exists(outputFilePath))
        {
            _logger.LogError("Failed to remux {Input} with audio transcode", inputFilePath);
            throw new InvalidOperationException("FFmpeg failed to remux video with audio transcode to AAC");
        }

        _logger.LogInformation("Remux with audio transcode completed: {Output} ({Size} bytes)",
            outputFilePath, new FileInfo(outputFilePath).Length);
    }

    private static int? ResolveScaleHeight(string? videoResolutionIdentifier)
    {
        if (string.IsNullOrEmpty(videoResolutionIdentifier) || videoResolutionIdentifier == "original")
            return null;

        var quality = Constants.VideoQualities.FirstOrDefault(kvp => kvp.Value.Name == videoResolutionIdentifier).Value;
        return quality?.Height;
    }

    private static void ApplyVideoFilterOrScale(
        FFMpegArgumentOptions options,
        VideoEncoderSelection? encoder,
        int? scaleHeight)
    {
        if (!string.IsNullOrWhiteSpace(encoder?.VideoFilter))
        {
            var filter = scaleHeight is int height
                ? $"scale=-2:{height},{encoder.VideoFilter}"
                : encoder.VideoFilter;
            options.WithCustomArgument($"-vf \"{filter}\"");
            return;
        }

        if (scaleHeight is int h)
            options.ConfigureVideoScalingHlsOptions(h);
    }
}

public class AutoScaleArgument : IVideoFilterArgument
{
    public readonly int Height;

    public string Key { get; } = "scale";

    public string Value
    {
        get => $"trunc(oh*a/2)*2:{Height}";
    }

    public AutoScaleArgument(int height)
    {
        Height = height;
    }
}

internal static class FFMpegArgumentsExtensions
{
    public static FFMpegArgumentOptions ConfigureSeekOptions(this FFMpegArgumentOptions options, HlsSegment firstSegment, HlsSegment lastSegment)
    {
        var startTimeStamp = TimeSpan.FromMilliseconds(firstSegment.StartTimestamp);

        return firstSegment != lastSegment
            ? options
                .Seek(startTimeStamp)
                .EndSeek(TimeSpan.FromMilliseconds(lastSegment.StartTimestamp + 10))
            : options
                .Seek(startTimeStamp);
    }

    public static FFMpegArgumentOptions ConfigureVideoScalingHlsOptions(this FFMpegArgumentOptions options, int? height)
    {
        return height is int targetHeight ?
            options.WithVideoFilters(filterOptions => filterOptions.Arguments.Add(new AutoScaleArgument(targetHeight)))
            : options;
    }
}
