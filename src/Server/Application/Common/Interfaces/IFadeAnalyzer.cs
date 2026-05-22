namespace K7.Server.Application.Common.Interfaces;

public interface IFadeAnalyzer
{
    /// <summary>
    /// Analyzes an audio file to determine fade-in and fade-out durations (seconds).
    /// FadeIn = time from start until signal rises above threshold.
    /// FadeOut = time before end where signal drops below threshold.
    /// </summary>
    Task<FadeAnalysisResult?> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default);
}

public sealed record FadeAnalysisResult(double FadeInDuration, double FadeOutDuration);
