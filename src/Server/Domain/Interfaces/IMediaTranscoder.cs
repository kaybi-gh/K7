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
    /// Starts a streaming transcode job with throttling support.
    /// Generates segments from startSegmentIndex up to (but not including) endSegmentIndex.
    /// Returns a task that represents the ffmpeg process execution.
    /// </summary>
    Task StartStreamingTranscodeAsync(
        string inputFilePath,
        string outputDirectory,
        List<HlsSegment> allSegments,
        int startSegmentIndex,
        int endSegmentIndex,
        CancellationToken cancellationToken,
        string? videoCodec = null,
        string? audioCodec = null,
        string? videoResolutionIdentifier = null);
}
