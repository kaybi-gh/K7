namespace K7.Server.Domain.Interfaces;

public interface ILoudnessAnalyzer
{
    Task<double?> AnalyzeLufsAsync(string filePath, CancellationToken cancellationToken = default);
}
