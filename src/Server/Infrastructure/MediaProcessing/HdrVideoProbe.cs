using FFMpegCore;

namespace K7.Server.Infrastructure.MediaProcessing;

/// <summary>
/// Detects HDR transfer characteristics via ffprobe for optional HDR-to-SDR tonemap.
/// Only PQ/HLG (and related) color_transfer values count as HDR. 10-bit HEVC Main 10
/// without HDR transfer tags is SDR and must not trigger tonemap (zscale fails with
/// "no path between colorspaces" when transfer is unspecified).
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
        return IsHdrTransfer(transfer);
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
}
