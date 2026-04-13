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
    /// When <paramref name="subtitleBurnInStreamIndex"/> is set, burns the bitmap subtitle into the video.
    /// When <paramref name="muxedAudioTrackIndex"/> is set, includes the specified audio track in the video segments
    /// instead of stripping audio (used for clients that don't support separate HLS audio renditions).
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
        int? muxedAudioTrackIndex = null,
        string? muxedAudioCodec = null);

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
}
