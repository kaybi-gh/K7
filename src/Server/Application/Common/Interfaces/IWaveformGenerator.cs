namespace K7.Server.Application.Common.Interfaces;

public interface IWaveformGenerator
{
    /// <summary>
    /// Generates normalized amplitude peaks (0.0–1.0) sampled across the audio file duration.
    /// </summary>
    Task<float[]?> GenerateAsync(string filePath, int peakCount = 200, CancellationToken cancellationToken = default);
}
