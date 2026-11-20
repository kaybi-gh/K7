using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities.Metadatas.Files;

namespace K7.Server.Application.Common.Interfaces;

public record SilencePeriod(double StartSeconds, double EndSeconds);

public interface ISegmentDetectionService
{
    Task<IReadOnlyList<SilencePeriod>> DetectSilenceAsync(
        string filePath,
        double startSeconds,
        double durationSeconds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<double>> DetectBlackFrameTimestampsAsync(
        string filePath,
        double startSeconds,
        double durationSeconds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<double>> DetectKeyframeTimestampsAsync(
        string filePath,
        double startSeconds,
        double durationSeconds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Samples the picture once per <see cref="CreditsFrameAnalyzer.SampleIntervalSeconds"/> and
    /// measures each frame for <see cref="CreditsFrameAnalyzer.FindCreditsRun"/>. The span is
    /// read from its end and only as far back as credits may go: earlier slots come back empty.
    /// Returns null when the file could not be decoded, as opposed to an empty list.
    /// </summary>
    Task<IReadOnlyList<CreditsFrameAnalyzer.FrameStats>?> SampleCreditsFramesAsync(
        string filePath,
        double startSeconds,
        double durationSeconds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<double>> GetChapterTimesAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ChapterMarker>> GetChaptersAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
