using FFMpegCore;

namespace K7.Server.Infrastructure.MediaProcessing;

/// <summary>
/// Detects HDR transfer characteristics via ffprobe for optional HDR-to-SDR tonemap.
/// </summary>
internal static class HdrVideoProbe
{
    private static readonly string[] HdrTransfers =
    [
        "smpte2084",
        "arib-std-b67",
        "smpte2094-40",
        "smpte2094-10"
    ];

    public static async Task<bool> IsHdrAsync(string inputFilePath, CancellationToken cancellationToken = default)
    {
        var transfer = await TryReadColorTransferAsync(inputFilePath, cancellationToken);
        if (IsHdrTransfer(transfer))
            return true;

        // Fallback when color_transfer is unset but the stream is clearly 10-bit HDR-ish.
        return await LooksLikeTenBitAsync(inputFilePath, cancellationToken);
    }

    public static bool IsHdrTransfer(string? colorTransfer)
    {
        if (string.IsNullOrWhiteSpace(colorTransfer))
            return false;

        foreach (var marker in HdrTransfers)
        {
            if (colorTransfer.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static async Task<string?> TryReadColorTransferAsync(
        string inputFilePath,
        CancellationToken cancellationToken)
    {
        string? transfer = null;

        await SafeProcessRunner.RunAsync(
            GlobalFFOptions.GetFFProbeBinaryPath(),
            "-loglevel error -select_streams v:0 -show_entries stream=color_transfer -of csv=p=0 "
            + "\""
            + inputFilePath
            + "\"",
            onStdout: line =>
            {
                if (!string.IsNullOrWhiteSpace(line) && transfer is null)
                    transfer = line.Trim();
            },
            timeout: TimeSpan.FromSeconds(30),
            cancellationToken: cancellationToken);

        return transfer;
    }

    private static async Task<bool> LooksLikeTenBitAsync(
        string inputFilePath,
        CancellationToken cancellationToken)
    {
        try
        {
            var analysis = await FFProbe.AnalyseAsync(inputFilePath, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
            var stream = analysis.PrimaryVideoStream;
            if (stream is null)
                return false;

            if (stream.BitDepth >= 10)
                return true;

            var pix = stream.PixelFormat;
            if (string.IsNullOrEmpty(pix))
                return false;

            return pix.Contains("p010", StringComparison.OrdinalIgnoreCase)
                || pix.Contains("yuv420p10", StringComparison.OrdinalIgnoreCase)
                || pix.Contains("yuv422p10", StringComparison.OrdinalIgnoreCase)
                || pix.Contains("yuv444p10", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}
