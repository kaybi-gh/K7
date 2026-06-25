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
        int? subtitleBurnInStreamIndex = null);

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
        string? audioCodec = null);

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
