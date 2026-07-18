using System.Globalization;
using FFMpegCore;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Entities;
using K7.Server.Application.Common.Configuration;
using K7.Server.Domain.Enums;
using K7.Shared;
using Microsoft.Extensions.Options;
using FFMpegCore.Enums;

namespace K7.Server.Infrastructure.MediaProcessing;

public class MediaAnalysisService : IMediaAnalysisService
{
    private readonly PathsConfiguration _pathsConfiguration;

    public MediaAnalysisService(IOptions<PathsConfiguration> pathsConfiguration)
    {
        _pathsConfiguration = pathsConfiguration.Value;
    }

    public async Task<AudioFileMetadata> GetAudioFileMetadataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var mediaAnalysis = await FFProbe.AnalyseAsync(filePath, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var primaryAudio = mediaAnalysis.PrimaryAudioStream
            ?? throw new InvalidOperationException("No audio stream found.");

        var audioTrack = new AudioFileTrack()
        {
            Index = primaryAudio.Index,
            IsDefault = true,
            Language = LanguageNormalizer.NormalizeOrPassthrough(primaryAudio.Language),
            Name = primaryAudio.Tags?.FirstOrDefault(t => t.Key == "title").Value ?? primaryAudio.Language,
            Codec = primaryAudio.CodecName,
            Channels = primaryAudio.Channels,
            ChannelLayout = primaryAudio.ChannelLayout,
            Profile = primaryAudio.Profile,
            SampleRateHz = primaryAudio.SampleRateHz
        };

        return new AudioFileMetadata()
        {
            Id = Guid.NewGuid(),
            Duration = mediaAnalysis.Duration,
            Container = GetMediaContainer(filePath, mediaAnalysis.Format.FormatName),
            AudioTrack = audioTrack
        };
    }

    public async Task<VideoFileMetadata> GetVideoFileMetadataAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var mediaAnalysis = await FFProbe.AnalyseAsync(filePath, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (mediaAnalysis.PrimaryVideoStream == null)
        {
            throw new InvalidOperationException();
        }

        var fileMetadata = new VideoFileMetadata()
        {
            Id = Guid.NewGuid(),
            Duration = mediaAnalysis.Duration,
            VideoBitrate = mediaAnalysis.PrimaryVideoStream.BitRate,
            VideoResolution = Constants.VideoQualities
                .OrderBy(x => Math.Abs(x.Value.Width - mediaAnalysis.PrimaryVideoStream.Width) +
                              Math.Abs(x.Value.Height - mediaAnalysis.PrimaryVideoStream.Height))
                .First().Key,
            Container = GetMediaContainer(filePath, mediaAnalysis.Format.FormatName),
            AudioTracks = ExtractAudioTracksFromMediaAnalysis(mediaAnalysis),
            VideoTracks = ExtractVideoTracksFromMediaAnalysis(mediaAnalysis),
            SubtitleTracks = ExtractSubtitleTracksFromMediaAnalysis(mediaAnalysis)
        };

        return fileMetadata;
    }

    public async Task<List<ChapterMarker>> GetChaptersAsync(string filePath, CancellationToken cancellationToken = default)
        => await ChapterProbe.ReadAsync(filePath, cancellationToken);

    private static string GetMediaContainer(string filePath, string formatName)
    {
        var formats = formatName.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (formats.Length == 1)
        {
            return formats[0];
        }

        var extension = Path.GetExtension(filePath)?.ToLower();
        if (string.IsNullOrEmpty(extension))
        {
            return formats[0];
        }

        if (Constants.ExtensionFormatMapping.TryGetValue(extension, out var format) && formats.Contains(format))
        {
            return format;
        }

        return formats[0];
    }

    private async static Task<List<long>> ExtractKeyframeTimestampsAsync(string path, CancellationToken cancellationToken = default)
    {
        var timestamps = new List<long>();

        await SafeProcessRunner.RunAsync(
            GlobalFFOptions.GetFFProbeBinaryPath(),
            $"-loglevel error -show_entries packet=pts_time,flags -of csv=print_section=0 -select_streams v:0 \"{path}\"",
            onStdout: line =>
            {
                if (line.Contains(",K", StringComparison.Ordinal)) // 'K' for keyframe
                {
                    var index = line.IndexOf(',', StringComparison.Ordinal);
                    if (index > 0)
                    {
                        var trimmedLine = line[..index];
                        if (double.TryParse(trimmedLine, NumberStyles.Float, CultureInfo.InvariantCulture, out var ptsTime))
                        {
                            timestamps.Add((long)(ptsTime * 1000)); // Convert to milliseconds
                        }
                    }
                }
            },
            timeout: TimeSpan.FromSeconds(300),
            cancellationToken: cancellationToken
        );

        return timestamps;
    }

    public async Task<List<HlsSegment>> ComputeKeyframeBasedHlsSegmentsAsync(
        IndexedFile indexedFile,
        TimeSpan segmentsDuration,
        long totalVideoDuration,
        CancellationToken cancellationToken = default
    )
    {
        var keyframeTimestamps = await ExtractKeyframeTimestampsAsync(indexedFile.Path, cancellationToken);

        if (keyframeTimestamps == null
            || keyframeTimestamps.Count == 0
            || indexedFile.FileMetadata == null)
        {
            return [];
        }

        var segments = new List<HlsSegment>();
        long segmentStart = 0;
        var nextSegmentBoundary = segmentStart + (long)segmentsDuration.TotalMilliseconds;

        for (int i = 1; i < keyframeTimestamps.Count; i++)
        {
            if (keyframeTimestamps[i] >= nextSegmentBoundary)
            {
                // Compare the keyframe timestamps to find the closest one to the boundary
                var segmentEnd = GetClosestTimestamp(keyframeTimestamps[i - 1], keyframeTimestamps[i], nextSegmentBoundary);

                // Ensure that the segment has a non-zero duration
                if (segmentEnd > segmentStart)
                {
                    segments.Add(new HlsSegment
                    {
                        FileMetadataId = indexedFile.FileMetadata.Id,
                        IndexedFileId = indexedFile.Id,
                        Number = segments.Count,
                        StartTimestamp = segmentStart,
                        Duration = segmentEnd - segmentStart
                    });

                    segmentStart = segmentEnd;
                    nextSegmentBoundary = segmentStart + (long)segmentsDuration.TotalMilliseconds;
                }
            }
        }

        // Handle the last segment and extend it to the totalVideoDuration
        long lastSegmentDuration = totalVideoDuration - segmentStart;

        if (lastSegmentDuration < segmentsDuration.TotalMilliseconds / 2 && segments.Count > 0)
        {
            // Merge the last short segment with the previous one
            var previousSegment = segments[^1];
            previousSegment.Duration = totalVideoDuration - previousSegment.StartTimestamp;
        }
        else
        {
            // Add the last segment
            segments.Add(new HlsSegment
            {
                FileMetadataId = indexedFile.FileMetadata.Id,
                IndexedFileId = indexedFile.Id,
                Number = segments.Count,
                StartTimestamp = segmentStart,
                Duration = lastSegmentDuration
            });
        }

        return segments;
    }

    public async Task<MetadataPicture> GenerateThumbnailsAsync(IndexedFile indexedFile, int delayBetweenTilesInSeconds = 30, CancellationToken cancellationToken = default)
    {
        if (indexedFile?.FileMetadata == null)
        {
            throw new InvalidOperationException();
        }

        var mediaInfo = await FFProbe.AnalyseAsync(indexedFile.Path, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (mediaInfo.Duration.TotalSeconds <= 10 * delayBetweenTilesInSeconds)
        {
            throw new InvalidOperationException("Indexed file total duration is not worth creating thumbnails.");
        }

        var outputPath = Path.Combine(_pathsConfiguration.Metadatas, "indexed-files", $"{indexedFile.Id}", "thumbnails.jpg");
        var totalFrames = (int)Math.Ceiling(mediaInfo.Duration.TotalSeconds / delayBetweenTilesInSeconds);
        var columns = 10;
        var rows = (int)Math.Ceiling(totalFrames / (double)columns);

        var outputFile = new FileInfo(outputPath);
        outputFile.Directory?.Create();

        await FFMpegArguments
            .FromFileInput(indexedFile.Path, verifyExists: false, options => options
                .WithHardwareAcceleration(HardwareAccelerationDevice.Auto)
                .WithCustomArgument("-skip_frame nokey"))
            .OutputToFile(outputPath, overwrite: true, options => options
                .WithCustomArgument($"-vf \"fps=1/{delayBetweenTilesInSeconds},scale=320:180:force_original_aspect_ratio=increase,crop=320:180,tile={columns}x{rows}\"")
                .WithFrameOutputCount(1)
                .WithCustomArgument("-q:v 5"))
            .CancellableThrough(cancellationToken, timeout: (int)TimeSpan.FromSeconds(300).TotalMilliseconds)
            .ProcessAsynchronously(throwOnError: true)
            .ConfigureAwait(false);

        if (!outputFile.Exists)
        {
            throw new Exception($"Failed to generate thumbnails.");
        }

        return new MetadataPicture()
        {
            Type = MetadataPictureType.Thumbnail,
            VideoFileMetadataId = indexedFile.FileMetadata.Id,
            LocalPath = outputPath
        };
    }

    private static long GetClosestTimestamp(long previousTimestamp, long nextTimestamp, long targetTimestamp)
    {
        long distanceToPreviousTimestamp = Math.Abs(previousTimestamp - targetTimestamp);
        long distanceToNextTimestamp = Math.Abs(nextTimestamp - targetTimestamp);
        return distanceToPreviousTimestamp <= distanceToNextTimestamp
            ? previousTimestamp
            : nextTimestamp;
    }

    private static List<AudioFileTrack> ExtractAudioTracksFromMediaAnalysis(IMediaAnalysis mediaAnalysis)
    {
        bool hasDefaultAudio = mediaAnalysis.AudioStreams.Any(s => s.Disposition?.Any(d => d.Key == "default" && d.Value) ?? false);
        return [.. mediaAnalysis.AudioStreams.Select(x => new AudioFileTrack()
        {
            Index = x.Index,
            IsDefault = IsDefaultTrack(hasDefaultAudio, x.Disposition, x.Index),
            Language = LanguageNormalizer.NormalizeOrPassthrough(x.Language),
            Name = x.Tags?.FirstOrDefault(x => x.Key == "title").Value ?? x.Language,
            Codec = x.CodecName,
            Channels = x.Channels,
            ChannelLayout = x.ChannelLayout,
            Profile = x.Profile,
            SampleRateHz = x.SampleRateHz
        })];
    }

    private static List<VideoFileTrack> ExtractVideoTracksFromMediaAnalysis(IMediaAnalysis mediaAnalysis)
    {
        var realVideoStreams = mediaAnalysis.VideoStreams
            .Where(x => !(x.Disposition?.Any(d => d.Key == "attached_pic" && d.Value) ?? false))
            .ToList();

        bool hasDefaultVideo = realVideoStreams.Any(s => s.Disposition?.Any(d => d.Key == "default" && d.Value) ?? false);
        
        return [.. realVideoStreams.Select(x => new VideoFileTrack
        {
            Codec = x.CodecName,
            Width = x.Width,
            Height = x.Height,
            Profile = x.Profile,
            Index = x.Index,
            BitDepth = x.BitDepth,
            Level = x.Level,
            IsDefault = IsDefaultTrack(hasDefaultVideo, x.Disposition, x.Index),
            PixelFormat = x.PixelFormat
        })];
    }

    private static bool IsDefaultTrack(bool hasDefaultTrack, IDictionary<string, bool>? disposition, int index)
    {
        return hasDefaultTrack ?
            disposition?.Any(x => x.Key == "default" && x.Value) ?? false
            : index == 0;
    }

    private static readonly HashSet<string> TextBasedSubtitleCodecs = new(StringComparer.OrdinalIgnoreCase)
    {
        "subrip", "srt", "ass", "ssa", "webvtt", "mov_text", "text", "ttml"
    };

    private static List<SubtitleFileTrack> ExtractSubtitleTracksFromMediaAnalysis(IMediaAnalysis mediaAnalysis)
    {
        bool hasDefaultSub = mediaAnalysis.SubtitleStreams.Any(s => s.Disposition?.Any(d => d.Key == "default" && d.Value) ?? false);
        return [.. mediaAnalysis.SubtitleStreams.Select(x =>
        {
            var name = x.Tags?.FirstOrDefault(t => t.Key == "title").Value ?? x.Language;
            return new SubtitleFileTrack
            {
                Index = x.Index,
                IsDefault = IsDefaultTrack(hasDefaultSub, x.Disposition, x.Index),
                Language = LanguageNormalizer.ResolveSubtitleLanguage(x.Language, name),
                Name = name,
                Codec = x.CodecName,
                IsTextBased = TextBasedSubtitleCodecs.Contains(x.CodecName),
                IsForced = x.Disposition?.Any(d => d.Key == "forced" && d.Value) ?? false,
                IsHearingImpaired = x.Disposition?.Any(d => d.Key == "hearing_impaired" && d.Value) ?? false
            };
        })];
    }
}
