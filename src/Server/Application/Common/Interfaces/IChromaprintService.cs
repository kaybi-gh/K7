namespace K7.Server.Application.Common.Interfaces;

public interface IChromaprintService
{
    Task<byte[]?> ExtractFingerprintAsync(
        string filePath,
        TimeSpan? startTime = null,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default);
}
