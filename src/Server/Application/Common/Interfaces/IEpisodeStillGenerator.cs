namespace K7.Server.Application.Common.Interfaces;

public record EpisodeStillGenerationResult(string FilePath, int Width, int Height, double TimestampSeconds);

public interface IEpisodeStillGenerator
{
    Task<EpisodeStillGenerationResult> GenerateAsync(
        string videoFilePath,
        string outputFilePath,
        double durationSeconds,
        double? introEndSeconds,
        CancellationToken cancellationToken = default);
}
