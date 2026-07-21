using System.Diagnostics;
using System.Runtime.ExceptionServices;
using K7.Clients.Shared.Interfaces;
using Microsoft.JSInterop;

namespace K7.Clients.MAUI.Services;

/// <summary>
/// Logs every JSException (including caught first-chance ones) so Debug Output
/// and a file show the actual JS error text Visual Studio otherwise hides, and
/// forwards a rate-limited sample of them to the server through
/// <see cref="IClientErrorReporter"/>, the same path used for other caught client errors.
/// </summary>
public static class JsExceptionDebugListener
{
    private const string ReportContext = "JSException";

    // First-chance handlers can fire thousands of times per second when something in a
    // loop keeps throwing (this is the exact scenario that motivated rate-limiting).
    // Keep both a per-error dedupe window and a hard cap on reports per minute so a
    // flood of JSExceptions can never spam the server.
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromMinutes(1);
    private const int MaxReportsPerWindow = 10;
    private const int MaxTrackedKeys = 200;

    private static readonly object ReportGate = new();
    private static readonly Dictionary<string, DateTime> LastReportedByKey = new();

    private static int _count;
    private static string? _logPath;
    private static int _installed;
    private static DateTime _windowStart;
    private static int _windowCount;

    public static string? LogPath => _logPath;

    public static void Install()
    {
        if (Interlocked.Exchange(ref _installed, 1) != 0)
            return;

        try
        {
            var dir = FileSystem.AppDataDirectory;
            Directory.CreateDirectory(dir);
            _logPath = Path.Combine(dir, "k7-js-exceptions.log");
            File.WriteAllText(_logPath, $"K7 JS exception log started {DateTime.Now:O}{Environment.NewLine}");
            Debug.WriteLine($"[K7-JS] Logging JSException details to {_logPath}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[K7-JS] Failed to create log file: {ex.Message}");
        }

        AppDomain.CurrentDomain.FirstChanceException += OnFirstChanceException;
    }

    private static void OnFirstChanceException(object? sender, FirstChanceExceptionEventArgs e)
    {
        if (e.Exception is not JSException jsEx)
            return;

        var n = Interlocked.Increment(ref _count);
        var line =
            $"[{DateTime.Now:HH:mm:ss.fff}] #{n} {jsEx.GetType().Name}: {jsEx.Message}";

        if (jsEx.InnerException is not null)
            line += $"{Environment.NewLine}  Inner: {jsEx.InnerException.GetType().Name}: {jsEx.InnerException.Message}";

        // Keep the top of the managed stack so we see which C# call issued the interop.
        var stack = jsEx.StackTrace;
        if (!string.IsNullOrEmpty(stack))
        {
            var frames = stack.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            var useful = frames.Where(f =>
                    f.Contains("K7.", StringComparison.Ordinal)
                    || f.Contains("Invoke", StringComparison.Ordinal)
                    || f.Contains("JSRuntime", StringComparison.Ordinal))
                .Take(8);
            line += $"{Environment.NewLine}  Stack:{Environment.NewLine}    {string.Join($"{Environment.NewLine}    ", useful)}";
        }

        Debug.WriteLine($"[K7-JS] {line}");

        var path = _logPath;
        if (path is not null)
        {
            try
            {
                File.AppendAllText(path, line + Environment.NewLine + "---" + Environment.NewLine);
            }
            catch
            {
                // Never throw from a first-chance handler.
            }
        }

        TryReportToServer(jsEx);
    }

    private static void TryReportToServer(JSException jsEx)
    {
        try
        {
            var dedupeKey = $"{jsEx.GetType().Name}:{jsEx.Message}:{jsEx.InnerException?.Message}";
            if (!ShouldReportToServer(dedupeKey))
                return;

            // FirstChanceException fires before MauiApp.Build() may have finished, so the
            // service provider is not guaranteed to exist yet. Resolving it lazily here
            // (rather than capturing it at Install time) means reporting "just works" once
            // DI is ready, which in practice is well before the WebView can raise a JSException.
            var reporter = IPlatformApplication.Current?.Services.GetService<IClientErrorReporter>();

            // Do not notify the user - this mirrors caught interop noise, not a user-facing failure.
            reporter?.ReportError(jsEx, ReportContext, notifyUser: false);
        }
        catch
        {
            // Never throw from a first-chance handler.
        }
    }

    private static bool ShouldReportToServer(string dedupeKey)
    {
        var now = DateTime.UtcNow;

        lock (ReportGate)
        {
            if (LastReportedByKey.TryGetValue(dedupeKey, out var lastReported)
                && now - lastReported < DedupeWindow)
            {
                return false;
            }

            if (now - _windowStart > RateLimitWindow)
            {
                _windowStart = now;
                _windowCount = 0;
            }

            if (_windowCount >= MaxReportsPerWindow)
                return false;

            _windowCount++;
            LastReportedByKey[dedupeKey] = now;

            if (LastReportedByKey.Count > MaxTrackedKeys)
            {
                var staleCutoff = now - DedupeWindow;
                foreach (var key in LastReportedByKey.Where(kvp => kvp.Value < staleCutoff).Select(kvp => kvp.Key).ToList())
                {
                    LastReportedByKey.Remove(key);
                }
            }

            return true;
        }
    }
}
