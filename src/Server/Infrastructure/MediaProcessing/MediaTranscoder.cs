using System.Diagnostics;
using System.Globalization;
using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing;

public class MediaTranscoder : IMediaTranscoder
{
    private readonly ILogger<MediaTranscoder> _logger;

    public MediaTranscoder(ILogger<MediaTranscoder> logger)
    {
        _logger = logger;
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

    public async Task StartStreamingTranscodeAsync(
        string inputFilePath,
        string outputDirectory,
        List<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        CancellationToken cancellationToken,
        string? videoCodec = null,
        string? audioCodec = null,
        string? videoResolutionIdentifier = null)
    {
        if (startSegmentIndex < 0 || endSegmentIndex > allSegments.Count || startSegmentIndex >= endSegmentIndex)
        {
            throw new ArgumentException("Invalid segment range");
        }

        var firstSegment = allSegments[startSegmentIndex];
        var lastSegment = allSegments[endSegmentIndex - 1];

        var startTime = TimeSpan.FromMilliseconds(firstSegment.StartTimestamp);
        var endTime = TimeSpan.FromMilliseconds(lastSegment.StartTimestamp + lastSegment.Duration);
        var duration = endTime - startTime;

        Directory.CreateDirectory(outputDirectory);

        // Determine if we need transcoding
        var needsVideoTranscode = !string.IsNullOrEmpty(videoCodec);
        var needsAudioTranscode = !string.IsNullOrEmpty(audioCodec);

        _logger.LogInformation(
            "Starting streaming transcode: input={Input}, output={Output}, startSeg={Start}, endSeg={End}, videoCodec={VideoCodec}, audioCodec={AudioCodec}",
            inputFilePath,
            outputDirectory,
            startSegmentIndex,
            endSegmentIndex,
            videoCodec ?? "copy",
            audioCodec ?? "copy");

        // Build ffmpeg command using FFMpegCore
        await FFMpegArguments
            .FromFileInput(inputFilePath, verifyExists: true, options => options
                .WithHardwareAcceleration(HardwareAccelerationDevice.Auto)
                .Seek(startTime))
            .OutputToFile("index.m3u8", overwrite: true, options =>
            {
                // Map video and audio streams explicitly
                options.WithCustomArgument("-map 0:v:0");
                options.WithCustomArgument("-map 0:a:0");

                // Video codec and settings
                if (needsVideoTranscode)
                {
                    if (videoCodec == "h264")
                    {
                        options.WithCustomArgument("-c:v h264")
                            .WithCustomArgument("-profile:v main")
                            .WithCustomArgument("-level:v 4.0")
                            .WithCustomArgument("-pix_fmt yuv420p");
                    }
                    else if (videoCodec == "hevc")
                    {
                        options.WithCustomArgument("-c:v libx265");
                    }

                    // Apply resolution scaling if specified
                    if (!string.IsNullOrEmpty(videoResolutionIdentifier) && videoResolutionIdentifier != "original")
                    {
                        var quality = Constants.VideoQualities.FirstOrDefault(kvp => kvp.Value.Name == videoResolutionIdentifier).Value;
                        if (quality?.Height is int height)
                        {
                            options.ConfigureVideoScalingHlsOptions(height);
                        }
                    }
                }
                else
                {
                    options.WithCustomArgument("-c:v copy");
                }

                // Audio codec and settings
                if (needsAudioTranscode)
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

                // Duration limit
                options.WithCustomArgument($"-t {duration.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");

                // HLS output format with fMP4
                options.WithCustomArgument("-f hls")
                    .WithCustomArgument("-hls_time 6")
                    .WithCustomArgument("-hls_segment_type fmp4")
                    .WithCustomArgument("-hls_flags independent_segments")
                    .WithCustomArgument($"-start_number {startSegmentIndex}")
                    .WithCustomArgument("-hls_fmp4_init_filename init.m4s")
                    .WithCustomArgument("-hls_segment_filename %d.m4s");
            })
            .Configure(options => options.WorkingDirectory = outputDirectory)
            .Configure(options => options.TemporaryFilesFolder = outputDirectory)
            .CancellableThrough(cancellationToken)
            .ProcessAsynchronously(throwOnError: false);

        _logger.LogInformation("FFmpeg process completed for output directory: {OutputDir}", outputDirectory);
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
        //.EndSeek(startTimeStamp.Add(TimeSpan.FromMilliseconds(firstSegment.Duration))); // TODO - Useful or not?
    }

    public static FFMpegArgumentOptions ConfigureGenericHlsOptions(this FFMpegArgumentOptions options, string tempDirectory, HlsSegment firstSegment)
    {
        // Deprecated: TS-based HLS generation has been replaced by MP4/CMAF-like segments.
        return options;
    }

    public static FFMpegArgumentOptions ConfigureVideoScalingHlsOptions(this FFMpegArgumentOptions options, int? height)
    {
        // TODO - Do we keep this method or not?
        return height is int targetHeight ?
            options.WithVideoFilters(filterOptions => filterOptions.Arguments.Add(new AutoScaleArgument(targetHeight)))
            : options;
    }
}
