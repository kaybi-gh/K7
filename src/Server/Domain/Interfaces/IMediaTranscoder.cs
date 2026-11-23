using System.Diagnostics;
using K7.Server.Domain.Entities;

namespace K7.Server.Domain.Interfaces;

public interface IMediaTranscoder
{
    Task RemuxVideoAsync(
        string inputFilePath,
        string tempDirectory,
        string outputFilePath,
        List<HlsSegment> segments,
        CancellationToken cancellationToken = default);

    Task TranscodeVideoAsync(
        string inputFilePath,
        string tempDirectory,
        string outputFilePath,
        List<HlsSegment> segments,
        string videoResolutionIdentifier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a video streaming transcode job.
    /// Generates video fMP4 segments from startSegmentIndex up to (but not including) endSegmentIndex.
    /// When <paramref name="subtitleBurnInStreamIndex"/> is set, burns the bitmap subtitle into the video via overlay.
    /// </summary>
    Task StartVideoStreamingTranscodeAsync(
        string inputFilePath,
        string outputDirectory,
        List<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        CancellationToken cancellationToken,
        string? videoCodec = null,
        string? videoResolutionIdentifier = null,
        int? subtitleBurnInStreamIndex = null,
        FfmpegMediaInput? input = null);

    /// <summary>
    /// Starts an audio-only streaming transcode job.
    /// Generates audio fMP4 segments from startSegmentIndex up to (but not including) endSegmentIndex.
    /// </summary>
    Task StartAudioStreamingTranscodeAsync(
        string inputFilePath,
        string outputDirectory,
        List<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        CancellationToken cancellationToken,
        int audioTrackIndex,
        string? audioCodec = null,
        int? audioChannels = null,
        FfmpegMediaInput? input = null);

    /// <summary>
    /// Extracts a subtitle track from the input file and converts it to WebVTT format.
    /// The result is cached at <paramref name="outputVttPath"/>.
    /// </summary>
    Task ExtractSubtitleAsVttAsync(
        string inputFilePath,
        int subtitleStreamIndex,
        string outputVttPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts several text subtitle tracks to WebVTT in a single read of the input file.
    /// Keys are stream indexes, values the output paths.
    /// </summary>
    Task ExtractSubtitlesAsVttAsync(
        string inputFilePath,
        IReadOnlyDictionary<int, string> outputVttPathsByStreamIndex,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Extracts the cues of a text subtitle track between <paramref name="start"/> and
    /// <paramref name="end"/> to WebVTT. ffmpeg seeks to the start through the container
    /// index, so a window far into a large file does not read it from the beginning. Cue
    /// times stay on the media timeline: they equal those of a full extraction.
    /// </summary>
    Task ExtractSubtitleWindowAsVttAsync(
        string inputFilePath,
        int subtitleStreamIndex,
        TimeSpan start,
        TimeSpan end,
        string outputVttPath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Copies a window of an embedded PGS subtitle track to <paramref name="output"/> as a
    /// lossless .sup stream. Timestamps stay on the media timeline, so a window that starts
    /// at 50s carries display sets stamped from 50s. Written as ffmpeg reads the file.
    /// </summary>
    Task ExtractPgsSubtitleWindowAsync(
        string inputFilePath,
        int subtitleStreamIndex,
        TimeSpan position,
        TimeSpan duration,
        Stream output,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Remuxes a video file, copying the video stream and transcoding the audio to AAC.
    /// Used for offline downloads when the source audio codec is not natively supported.
    /// </summary>
    Task RemuxWithAudioTranscodeAsync(
        string inputFilePath,
        string outputFilePath,
        int audioTrackIndex,
        int[]? subtitleTrackIndices = null,
        CancellationToken cancellationToken = default);
}
