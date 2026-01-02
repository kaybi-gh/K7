using K7.Server.Domain.Constants;

namespace K7.Server.Domain.Interfaces;

public interface IMediaTranscoder
{
    Task GenerateTranscodedAudioHlsSegmentAsync(
        string inputFilePath,
        string tempDirectory,
        string outputFilePath,
        List<HlsSegment> segments,
        int fileStreamIndex,
        AudioQuality audioQuality,
        CancellationToken cancellationToken = default);

    Task GenerateRemuxedAudioHlsSegmentAsync(
        string inputFilePath,
        string tempDirectory,
        string outputFilePath,
        List<HlsSegment> segments,
        int fileStreamIndex,
        CancellationToken cancellationToken = default);

    Task GenerateRemuxedVideoHlsSegmentAsync(
        string inputFilePath,
        string tempDirectory,
        string outputFilePath,
        List<HlsSegment> segments,
        CancellationToken cancellationToken = default);

    Task GenerateTranscodedVideoHlsSegmentAsync(
        string inputFilePath,
        string tempDirectory,
        string outputFilePath,
        List<HlsSegment> segments,
        string videoResolutionIdentifier,
        CancellationToken cancellationToken = default);
}
