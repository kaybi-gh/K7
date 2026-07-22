using System.Diagnostics;
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
            var inputArgs = new List<string>();
            if (startTime.HasValue)
                inputArgs.Add($"-ss {startTime.Value.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
            if (duration.HasValue)
                inputArgs.Add($"-t {duration.Value.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)}");
            inputArgs.Add($"-i \"{filePath}\"");

            // Chromaprint is audio-only. Without -vn/-map/-ac, FFmpeg can fail immediately
            // (exit 234 / EINVAL) on multi-stream MKV (video + 5.1 + PGS subtitles).
            var args = string.Join(" ", inputArgs) + " -vn -map 0:a:0 -ac 1 -f chromaprint -fp_format raw -";

            // FFmpeg's -fp_format raw outputs uint32 values as raw binary to stdout.
            // We must read stdout as a binary stream, not as text lines.
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = GlobalFFOptions.GetFFMpegBinaryPath(),
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = false,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            if (!process.Start())
            {
                logger.LogWarning("Failed to start FFmpeg for chromaprint extraction");
                return null;
            }

            // Read binary stdout and drain stderr concurrently
            var stdoutTask = ReadBinaryStdoutAsync(process, cancellationToken);
            var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(120));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Chromaprint extraction timed out for '{FilePath}'", filePath);
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                    logger.LogDebug(ex, "Chromaprint process kill failed for '{FilePath}'", filePath);
                }
                return null;
            }

            var rawBytes = await stdoutTask;
            var stderr = await stderrTask;

            if (process.ExitCode != 0)
            {
                logger.LogWarning(
                    "Chromaprint extraction failed for '{FilePath}' with exit code {ExitCode}: {Stderr}",
                    filePath,
                    process.ExitCode,
                    TruncateStderr(stderr));
                return null;
            }

            // Raw fingerprint is an array of uint32 (4 bytes each)
            var fingerprintLength = rawBytes.Length / sizeof(uint) * sizeof(uint);
            if (fingerprintLength == 0)
            {
                logger.LogWarning("Chromaprint produced 0 fingerprint points for '{FilePath}' ({RawBytes} raw bytes)", filePath, rawBytes.Length);
                return null;
            }

            if (fingerprintLength != rawBytes.Length)
                rawBytes = rawBytes[..fingerprintLength];

            logger.LogDebug("Chromaprint extracted {Points} fingerprint points for '{FilePath}'", fingerprintLength / sizeof(uint), filePath);
            return rawBytes;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Chromaprint extraction failed for '{FilePath}'", filePath);
            return null;
        }
    }

    private static async Task<byte[]> ReadBinaryStdoutAsync(Process process, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await process.StandardOutput.BaseStream.CopyToAsync(ms, cancellationToken);
        return ms.ToArray();
    }

    private static string TruncateStderr(string stderr)
    {
        const int maxLength = 500;
        if (string.IsNullOrWhiteSpace(stderr))
            return "(empty)";

        var trimmed = stderr.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + "...";
    }
}
