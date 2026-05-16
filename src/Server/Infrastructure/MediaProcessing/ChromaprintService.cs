using System.Globalization;
using FFMpegCore;
using K7.Server.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.MediaProcessing;

public class ChromaprintService(ILogger<ChromaprintService> logger) : IChromaprintService
{
    public async Task<byte[]?> ExtractFingerprintAsync(
        string filePath,
        TimeSpan? startTime = null,
        TimeSpan? duration = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath))
        {
            logger.LogWarning("Chromaprint extraction skipped: file not found at '{FilePath}'", filePath);
            return null;
        }

        try
        {
            var rawOutput = new List<string>();

            var inputArgs = new List<string>();
            if (startTime.HasValue)
                inputArgs.Add($"-ss {startTime.Value.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
            if (duration.HasValue)
                inputArgs.Add($"-t {duration.Value.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
            inputArgs.Add($"-i \"{filePath}\"");

            var args = string.Join(" ", inputArgs) + " -f chromaprint -fp_format raw -";

            var exitCode = await SafeProcessRunner.RunAsync(
                GlobalFFOptions.GetFFMpegBinaryPath(),
                args,
                onStdout: line => rawOutput.Add(line),
                onStderr: line =>
                {
                    if (line.Contains("error", StringComparison.OrdinalIgnoreCase))
                        logger.LogDebug("FFmpeg chromaprint stderr: {Line}", line);
                },
                timeout: TimeSpan.FromSeconds(120),
                cancellationToken: cancellationToken
            );

            if (exitCode != 0)
            {
                logger.LogWarning("Chromaprint extraction failed for '{FilePath}' with exit code {ExitCode}", filePath, exitCode);
                return null;
            }

            return ParseRawFingerprint(rawOutput);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Chromaprint extraction failed for '{FilePath}'", filePath);
            return null;
        }
    }

    private static byte[]? ParseRawFingerprint(List<string> rawOutput)
    {
        if (rawOutput.Count == 0)
            return null;

        var combined = string.Join("", rawOutput).Trim();
        if (string.IsNullOrEmpty(combined))
            return null;

        // Raw chromaprint format outputs comma-separated integers
        var parts = combined.Split(',', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return null;

        using var ms = new MemoryStream(parts.Length * sizeof(int));
        using var writer = new BinaryWriter(ms);

        foreach (var part in parts)
        {
            if (int.TryParse(part.Trim(), out var value))
                writer.Write(value);
        }

        return ms.ToArray();
    }
}
