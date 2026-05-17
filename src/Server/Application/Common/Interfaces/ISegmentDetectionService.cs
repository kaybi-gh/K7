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

    Task<IReadOnlyList<double>> GetChapterTimesAsync(
        string filePath,
        CancellationToken cancellationToken = default);
}
