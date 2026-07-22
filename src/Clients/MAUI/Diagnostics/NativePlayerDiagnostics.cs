using Microsoft.Extensions.Logging;

namespace K7.Clients.MAUI.Diagnostics;

/// <summary>
/// High-signal native MediaElement playback diagnostics for adb logcat.
/// Tag: K7-NativePlayer (grep / adb -s K7-NativePlayer).
/// </summary>
internal static class NativePlayerDiagnostics
{
    public const string Tag = "K7-NativePlayer";

    private static readonly HashSet<string> SensitiveQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "token",
        "access_token",
        "ephemeral_token",
        "refresh_token",
        "authorization",
        "api_key",
        "apikey",
        "key",
        "sig",
        "signature"
    };

    public static void Info(ILogger? logger, string message)
    {
        logger?.LogInformation("{NativePlayerTag} {Message}", Tag, message);
        WritePlatform(LogLevel.Information, message);
    }

    public static void Warn(ILogger? logger, string message)
    {
        logger?.LogWarning("{NativePlayerTag} {Message}", Tag, message);
        WritePlatform(LogLevel.Warning, message);
    }

    public static void Error(ILogger? logger, string message)
    {
        logger?.LogError("{NativePlayerTag} {Message}", Tag, message);
        WritePlatform(LogLevel.Error, message);
    }

    /// <summary>
    /// Redacts sensitive query values while keeping parameter names for diagnostics.
    /// Example: ?access_token=abc&amp;Quality=720p -> ?access_token=REDACTED&amp;Quality=720p
    /// </summary>
    public static string RedactUrl(string? url)
    {
        if (string.IsNullOrEmpty(url))
            return "(null)";

        var queryIndex = url.IndexOf('?');
        if (queryIndex < 0)
            return url;

        var path = url[..queryIndex];
        var query = url[(queryIndex + 1)..];
        if (string.IsNullOrEmpty(query))
            return url;

        var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var eq = part.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = part[..eq];
            if (IsSensitiveQueryKey(key))
                parts[i] = key + "=REDACTED";
        }

        return path + "?" + string.Join('&', parts);
    }

    private static bool IsSensitiveQueryKey(string key)
    {
        if (SensitiveQueryKeys.Contains(key))
            return true;

        // Catch variants like DefaultAccessToken / streamToken without listing every alias.
        return key.Contains("token", StringComparison.OrdinalIgnoreCase)
            || key.Contains("auth", StringComparison.OrdinalIgnoreCase);
    }

    private static void WritePlatform(LogLevel level, string message)
    {
#if ANDROID
        switch (level)
        {
            case LogLevel.Warning:
                Android.Util.Log.Warn(Tag, message);
                break;
            case LogLevel.Error:
            case LogLevel.Critical:
                Android.Util.Log.Error(Tag, message);
                break;
            default:
                Android.Util.Log.Info(Tag, message);
                break;
        }
#else
        System.Diagnostics.Debug.WriteLine($"[{Tag}] {message}");
#endif
    }
}
