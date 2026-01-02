using FFMpegCore;
using FFMpegCore.Arguments;
using FFMpegCore.Enums;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Interfaces;
using AudioQuality = K7.Server.Domain.Constants.AudioQuality;

namespace K7.Server.Infrastructure.MediaProcessing;

public class MediaTranscoder : IMediaTranscoder
{
    public async Task GenerateTranscodedAudioHlsSegmentAsync(
        string inputFilePath,
        string tempDirectory,
        string outputFilePath,
        List<HlsSegment> segments,
        int fileStreamIndex,
        AudioQuality audioQuality,
        CancellationToken cancellationToken = default)
    {
        var firstSegment = segments.First();
        var lastSegment = segments.Last();

        await FFMpegArguments
            .FromFileInput(inputFilePath, verifyExists: true, options => options
                .WithHardwareAcceleration(HardwareAccelerationDevice.Auto)
               .ConfigureSeekOptions(firstSegment, lastSegment))
            .OutputToFile(Path.Combine(tempDirectory, "output.m3u8"), overwrite: false, options => options
                .ConfigureGenericHlsOptions(tempDirectory, firstSegment)
                .SelectStream(fileStreamIndex)
                .WithAudioCodec(AudioCodec.Aac)
                .WithCustomArgument($"-ac  6"))
            .CancellableThrough(cancellationToken, timeout: (int)TimeSpan.FromSeconds(30).TotalMilliseconds)
            .ProcessAsynchronously(throwOnError: true)
            .ConfigureAwait(false);

        var segmentFile = new FileInfo(Path.Combine(tempDirectory, $"{firstSegment.Number}.ts"));
        if (segmentFile.Exists)
        {
            File.Move(segmentFile.FullName, outputFilePath);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    public async Task GenerateRemuxedAudioHlsSegmentAsync(
        string inputFilePath,
        string tempDirectory,
        string outputFilePath,
        List<HlsSegment> segments,
        int fileStreamIndex,
        CancellationToken cancellationToken = default)
    {
        var firstSegment = segments.First();
        var lastSegment = segments.Last();

        await FFMpegArguments
            .FromFileInput(inputFilePath, verifyExists: true, options => options
                .WithHardwareAcceleration(HardwareAccelerationDevice.Auto)
                .ConfigureSeekOptions(firstSegment, lastSegment))
            .OutputToFile(Path.Combine(tempDirectory, "output.m3u8"), overwrite: false, options => options
                .ConfigureGenericHlsOptions(tempDirectory, firstSegment)
                .SelectStream(fileStreamIndex)
                .CopyChannel(Channel.Audio))
            .CancellableThrough(cancellationToken, timeout: (int)TimeSpan.FromSeconds(30).TotalMilliseconds)
            .ProcessAsynchronously(throwOnError: true)
            .ConfigureAwait(false);

        var segmentFile = new FileInfo(Path.Combine(tempDirectory, $"{firstSegment.Number}.ts"));
        if (segmentFile.Exists)
        {
            File.Move(segmentFile.FullName, outputFilePath);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    public async Task GenerateRemuxedVideoHlsSegmentAsync(
        string inputFilePath,
        string tempDirectory,
        string outputFilePath,
        List<HlsSegment> segments,
        CancellationToken cancellationToken = default)
    {
        var firstSegment = segments.First();
        var lastSegment = segments.Last();

        await FFMpegArguments
            .FromFileInput(inputFilePath, verifyExists: true, options => options
                .WithHardwareAcceleration(HardwareAccelerationDevice.Auto)
                .ConfigureSeekOptions(firstSegment, lastSegment))
            .OutputToFile(Path.Combine(tempDirectory, "output.m3u8"), overwrite: false, options => options
                .ConfigureGenericHlsOptions(tempDirectory, firstSegment)
                .SelectStream(0, channel: Channel.Video)
                .CopyChannel(Channel.Video))
            .CancellableThrough(cancellationToken, timeout: (int)TimeSpan.FromSeconds(30).TotalMilliseconds)
            .ProcessAsynchronously(throwOnError: true)
            .ConfigureAwait(false);

        var segmentFile = new FileInfo(Path.Combine(tempDirectory, $"{firstSegment.Number}.ts"));
        if (segmentFile.Exists)
        {
            File.Move(segmentFile.FullName, outputFilePath);
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    public async Task GenerateTranscodedVideoHlsSegmentAsync(
        string inputFilePath,
        string tempDirectory,
        string outputFilePath,
        List<HlsSegment> segments,
        string videoResolutionIdentifier,
        CancellationToken cancellationToken = default)
    {
        var quality = Constants.VideoQualities.Where(kvp => kvp.Value.Name == videoResolutionIdentifier).FirstOrDefault().Value;
        var firstSegment = segments.First();
        var lastSegment = segments.Last();

        await FFMpegArguments
            .FromFileInput(inputFilePath, verifyExists: true, options => options
                .WithHardwareAcceleration(HardwareAccelerationDevice.Auto)
               .ConfigureSeekOptions(firstSegment, lastSegment))
            .OutputToFile(Path.Combine(tempDirectory, "output.m3u8"), overwrite: false, options => options
                .ConfigureGenericHlsOptions(tempDirectory, firstSegment)
                .SelectStream(0, channel: Channel.Video)
                .WithVideoCodec(VideoCodec.LibX264)
                .ConfigureVideoScalingHlsOptions(quality?.Height))
            .CancellableThrough(cancellationToken, timeout: (int)TimeSpan.FromSeconds(30).TotalMilliseconds)
            .ProcessAsynchronously(throwOnError: true)
            .ConfigureAwait(false);

        var segmentFile = new FileInfo(Path.Combine(tempDirectory, $"{firstSegment.Number}.ts"));
        if (segmentFile.Exists)
        {
            File.Move(segmentFile.FullName, outputFilePath);
            Directory.Delete(tempDirectory, recursive: true);
        }
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
        return options
            .WithStartNumber(firstSegment.Number)
            .WithCustomArgument($"-hls_time {firstSegment.Duration}ms")
            .WithCustomArgument("-hls_list_size 0")
            .WithCustomArgument("-copyts")
            .WithCustomArgument($"-hls_segment_filename {Path.Combine(tempDirectory, "%01d.ts")}");
    }

    public static FFMpegArgumentOptions ConfigureVideoScalingHlsOptions(this FFMpegArgumentOptions options, int? height)
    {
        // TODO - Do we keep this method or not?
        return height is int targetHeight ?
            options.WithVideoFilters(filterOptions => filterOptions.Arguments.Add(new AutoScaleArgument(targetHeight)))
            : options;
    }
}
