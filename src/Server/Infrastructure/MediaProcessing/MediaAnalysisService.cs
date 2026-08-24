using FFMpegCore;
using FFMpegCore.Enums;
using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Entities.Metadatas.Files.Tracks;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Helpers;
using K7.Server.Domain.Interfaces;
using K7.Shared;
using Microsoft.Extensions.Options;

namespace K7.Server.Infrastructure.MediaProcessing;

public class MediaAnalysisService : IMediaAnalysisService
{
    private readonly PathsConfiguration _pathsConfiguration;

    public MediaAnalysisService(IOptions<PathsConfiguration> pathsConfiguration)
    {
        _pathsConfiguration = pathsConfiguration.Value;
    }

    private delegate bool TryParseKeyframeLine(string line, out long timestampMs);

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
            Name = AudioTrackDisplayHelper.ResolveStoredName(
                primaryAudio.Tags?.FirstOrDefault(t => t.Key == "title").Value,
                primaryAudio.Language),
            Codec = primaryAudio.CodecName ?? string.Empty,
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

    private static async Task<List<long>> ExtractKeyframeTimestampsAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        var quotedPath = $"\"{path}\"";
        var packetTimestamps = await RunKeyframeProbeAsync(
            $"-loglevel error -show_entries packet=pts_time,dts_time,flags -of csv=print_section=0 -select_streams v:0 {quotedPath}",
            HlsKeyframeTimestampParser.TryParsePacketLine,
            cancellationToken);

        if (packetTimestamps.Count > 0)
            return packetTimestamps;

        return await RunKeyframeProbeAsync(
            $"-loglevel error -skip_frame nokey -select_streams v:0 -show_entries frame=pts_time,pkt_pts_time,pkt_dts_time -of csv=print_section=0 {quotedPath}",
            HlsKeyframeTimestampParser.TryParseKeyframeFrameLine,
            cancellationToken);
    }

    private static async Task<List<long>> RunKeyframeProbeAsync(
        string arguments,
        TryParseKeyframeLine parseLine,
        CancellationToken cancellationToken)
    {
        var timestamps = new List<long>();
        var exitCode = await SafeProcessRunner.RunAsync(
            GlobalFFOptions.GetFFProbeBinaryPath(),
            arguments,
            onStdout: line =>
            {
                if (parseLine(line, out var timestampMs))
                    timestamps.Add(timestampMs);
            },
            timeout: TimeSpan.FromSeconds(300),
            cancellationToken: cancellationToken);

        if (exitCode != 0)
            throw new InvalidOperationException($"ffprobe failed while extracting keyframes (exit {exitCode}).");

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

        if (indexedFile.FileMetadata == null)
            return [];

        // segmentsDuration retained for API compatibility. Video playlists use every source
        // keyframe (shared with demuxed audio). Only collapse pathological micro-GOPs.
        _ = segmentsDuration;

        return HlsKeyframeSegmentBuilder.BuildFromTimestamps(
            keyframeTimestamps ?? [],
            totalVideoDuration,
            indexedFile.FileMetadata.Id,
            indexedFile.Id);
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

    private static List<AudioFileTrack> ExtractAudioTracksFromMediaAnalysis(IMediaAnalysis mediaAnalysis)
    {
        bool hasDefaultAudio = mediaAnalysis.AudioStreams.Any(s => s.Disposition?.Any(d => d.Key == "default" && d.Value) ?? false);
        return [.. mediaAnalysis.AudioStreams.Select(x => new AudioFileTrack()
        {
            Index = x.Index,
            IsDefault = IsDefaultTrack(hasDefaultAudio, x.Disposition, x.Index),
            Language = LanguageNormalizer.NormalizeOrPassthrough(x.Language),
            Name = AudioTrackDisplayHelper.ResolveStoredName(
                x.Tags?.FirstOrDefault(t => t.Key == "title").Value,
                x.Language),
            Codec = x.CodecName ?? string.Empty,
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
            Codec = x.CodecName ?? string.Empty,
            Width = x.Width,
            Height = x.Height,
            Profile = x.Profile ?? string.Empty,
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
            var title = x.Tags?.FirstOrDefault(t => t.Key == "title").Value;
            var name = AudioTrackDisplayHelper.ResolveStoredName(title, x.Language);
            return new SubtitleFileTrack
            {
                Index = x.Index,
                IsDefault = IsDefaultTrack(hasDefaultSub, x.Disposition, x.Index),
                Language = LanguageNormalizer.ResolveSubtitleLanguage(x.Language, title ?? x.Language),
                Name = name,
                Codec = x.CodecName ?? string.Empty,
                IsTextBased = x.CodecName is not null && TextBasedSubtitleCodecs.Contains(x.CodecName),
                IsForced = IsForcedSubtitle(x.Disposition, title ?? name),
                IsHearingImpaired = IsHearingImpairedSubtitle(x.Disposition, title ?? name)
            };
        })];
    }

    /// <summary>
    /// Prefer container disposition; many rips only put Forced/Force/Forcé in the track title.
    /// </summary>
    internal static bool IsForcedSubtitle(IDictionary<string, bool>? disposition, string? title)
    {
        if (disposition?.Any(d => d.Key == "forced" && d.Value) == true)
            return true;

        if (string.IsNullOrWhiteSpace(title))
            return false;

        if (ContainsToken(title, "non-forced")
            || ContainsToken(title, "nonforced")
            || ContainsToken(title, "non forced"))
        {
            return false;
        }

        return ContainsToken(title, "forced")
            || ContainsToken(title, "forcé");
    }

    internal static bool IsHearingImpairedSubtitle(IDictionary<string, bool>? disposition, string? title)
    {
        if (disposition?.Any(d => d.Key == "hearing_impaired" && d.Value) == true)
            return true;

        if (string.IsNullOrWhiteSpace(title))
            return false;

        return ContainsToken(title, "sdh")
            || ContainsToken(title, "hearing impaired");
    }

    private static bool ContainsToken(string value, string token)
    {
        var index = value.IndexOf(token, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
            return false;

        // Require token boundaries so "hi" does not match inside "French".
        var beforeOk = index == 0 || !char.IsLetterOrDigit(value[index - 1]);
        var afterIndex = index + token.Length;
        var afterOk = afterIndex >= value.Length || !char.IsLetterOrDigit(value[afterIndex]);
        return beforeOk && afterOk;
    }
}
