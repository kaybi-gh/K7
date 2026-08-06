namespace K7.Server.Application.Features.OpenSubsonic;

/// <summary>
/// Decides whether OpenSubsonic /rest/stream should transcode and with which format/bitrate.
/// </summary>
public static class OpenSubsonicStreamTranscode
{
    private static readonly HashSet<string> LosslessExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".flac", ".wav", ".aiff", ".aif", ".alac", ".wv", ".dsf", ".dff", ".ape"
    };

    public static bool TryResolve(
        bool download,
        string? format,
        int? maxBitRateKbps,
        int timeOffsetSeconds,
        string? sourceExtension,
        long fileSizeBytes,
        double durationSeconds,
        out string outputFormat,
        out int bitrateKbps)
    {
        outputFormat = "mp3";
        bitrateKbps = 192;

        if (download)
            return false;

        var fmt = string.IsNullOrWhiteSpace(format) ? null : format.Trim().ToLowerInvariant();
        if (fmt is "raw")
            return false;

        var estimatedKbps = durationSeconds > 0
            ? (int)Math.Round(fileSizeBytes * 8.0 / durationSeconds / 1000.0)
            : 0;

        var wantsFormat = fmt is "mp3" or "aac" or "opus" or "ogg";
        var formatMismatch = wantsFormat && !SourceMatches(fmt!, sourceExtension);
        var sourceIsLossless = IsLossless(sourceExtension);
        var bitrateExceeds = maxBitRateKbps is > 0 && estimatedKbps > maxBitRateKbps.Value;
        var forceBitrateOnLossless = maxBitRateKbps is > 0 && sourceIsLossless;

        if (!formatMismatch && !bitrateExceeds && !forceBitrateOnLossless)
            return false;

        outputFormat = wantsFormat ? fmt! : "mp3";
        bitrateKbps = maxBitRateKbps is > 0
            ? Math.Clamp(maxBitRateKbps.Value, 32, 320)
            : 192;

        // timeOffset is applied by the transcoder when this returns true (transcodeOffset).
        _ = timeOffsetSeconds;
        return true;
    }

    public static string ContentTypeFor(string format) =>
        format.ToLowerInvariant() switch
        {
            "aac" => "audio/aac",
            "opus" => "audio/ogg",
            "ogg" => "audio/ogg",
            _ => "audio/mpeg"
        };

    public static string ExtensionFor(string format) =>
        format.ToLowerInvariant() switch
        {
            "aac" => ".aac",
            "opus" => ".opus",
            "ogg" => ".ogg",
            _ => ".mp3"
        };

    private static bool IsLossless(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        var ext = extension.StartsWith('.') ? extension : "." + extension;
        return LosslessExtensions.Contains(ext);
    }

    private static bool SourceMatches(string format, string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
            return false;

        var ext = extension.TrimStart('.').ToLowerInvariant();
        return format switch
        {
            "mp3" => ext is "mp3",
            "aac" => ext is "aac" or "m4a" or "mp4",
            "opus" => ext is "opus" or "ogg",
            "ogg" => ext is "ogg" or "oga",
            _ => false
        };
    }
}
