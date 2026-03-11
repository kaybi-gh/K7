using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Application.Common.Interfaces;

public interface IAudioAnalyzer
{
    /// <summary>
    /// Analyzes an audio file and returns a populated AudioAnalysis, or null if analysis is unavailable.
    /// </summary>
    Task<AudioAnalysis?> AnalyzeAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Whether the analyzer is available (binary found, enabled in config).
    /// </summary>
    bool IsAvailable { get; }
}
