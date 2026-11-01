using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Services;

/// <summary>
/// Opt-in timing lines for the pages under optimization, enabled per device by an admin
/// (Settings -> About -> Diagnostics). Off by default: every entry point returns after reading
/// one field, so the calls can stay in Release builds and measure exactly what ships.
/// Times are relative to the last navigation, so the lines of one page read as a sequence:
/// <c>nav#3 /movies/... | nav#3 http GET /api/medias/... 200 182ms ... +190ms | nav#3 Movie ready +240ms</c>.
/// MAUI writes to logcat (<c>adb logcat -s K7-Perf</c>); other hosts write to the console.
/// </summary>
public static class PerfTrace
{
    private const string Prefix = "K7-Perf";
    private static readonly object _gate = new();
    private static readonly HashSet<string> _marks = new(StringComparer.Ordinal);
    private static volatile bool _enabled;
    private static long _navigationStart = Stopwatch.GetTimestamp();
    private static int _navigationId;
    private static int _initialized;

    /// <summary>Host hook receiving each line without the prefix (MAUI: logcat tag K7-Perf).</summary>
    public static Action<string>? Sink { get; set; }

    public static bool IsEnabled => _enabled;

    [DynamicDependency(nameof(ReportFromJs), typeof(PerfTrace))]
    public static void SetEnabled(bool enabled)
    {
        if (_enabled == enabled)
            return;

        _enabled = enabled;
        Emit(enabled ? "enabled" : "disabled");
    }

    /// <summary>
    /// Applies the device switch and follows navigations. Called by every root layout (EmptyLayout
    /// hosts the profile picker before MainLayout exists); only the first call subscribes.
    /// </summary>
    public static void Initialize(bool enabled, NavigationManager navigation)
    {
        SetEnabled(enabled);
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
            return;

        NavigationStarted(navigation.Uri);
        navigation.LocationChanged += (_, e) => NavigationStarted(e.Location);
    }

    /// <summary>Turns the JS side (D-pad latency) on or off to match <see cref="IsEnabled"/>.</summary>
    public static async Task SyncJsAsync(IJSRuntime js)
    {
        try
        {
            await js.InvokeVoidAsync("K7.PerfTrace.setEnabled", _enabled);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or TaskCanceledException)
        {
            // Script not loaded yet or runtime gone: measurements stay .NET-only.
        }
    }

    /// <summary>Starts a new measurement sequence (called on every location change).</summary>
    public static void NavigationStarted(string location)
    {
        if (!_enabled)
            return;

        int id;
        lock (_gate)
        {
            id = ++_navigationId;
            _navigationStart = Stopwatch.GetTimestamp();
            _marks.Clear();
        }

        Emit("nav#" + id + " " + StripOrigin(location));
    }

    /// <summary>Logs the first <paramref name="step"/> of <paramref name="page"/> since the last navigation.</summary>
    public static void Mark(string page, string step)
    {
        if (!_enabled)
            return;

        int id;
        double elapsedMs;
        lock (_gate)
        {
            if (!_marks.Add(page + ":" + step))
                return;

            id = _navigationId;
            elapsedMs = Stopwatch.GetElapsedTime(_navigationStart).TotalMilliseconds;
        }

        Emit("nav#" + id + " " + page + " " + step + " +" + FormatMs(elapsedMs));
    }

    /// <summary>Logs a free-form diagnostic line every time (no per-navigation dedup).</summary>
    public static void Note(string text)
    {
        if (!_enabled)
            return;

        int id;
        double elapsedMs;
        lock (_gate)
        {
            id = _navigationId;
            elapsedMs = Stopwatch.GetElapsedTime(_navigationStart).TotalMilliseconds;
        }

        Emit("nav#" + id + " " + text + " +" + FormatMs(elapsedMs));
    }

    public static void Request(string method, string? path, int? statusCode, TimeSpan duration, long? contentLength)
    {
        if (!_enabled)
            return;

        int id;
        double sinceNavigationMs;
        lock (_gate)
        {
            id = _navigationId;
            sinceNavigationMs = Stopwatch.GetElapsedTime(_navigationStart).TotalMilliseconds;
        }

        var status = statusCode?.ToString(CultureInfo.InvariantCulture) ?? "failed";
        var size = contentLength is { } bytes
            ? (bytes / 1024d).ToString("F1", CultureInfo.InvariantCulture) + "KB"
            : "?KB";

        Emit("nav#" + id + " http " + method + " " + path + " " + status + " " + FormatMs(duration.TotalMilliseconds)
            + " " + size + " +" + FormatMs(sinceNavigationMs));
    }

    /// <summary>Summaries computed in JS (D-pad latency), see <c>K7.PerfTrace</c> in navigation.js.</summary>
    [JSInvokable("ReportPerfTrace")]
    public static void ReportFromJs(string line)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(line))
            return;

        Emit("nav#" + Volatile.Read(ref _navigationId) + " " + line);
    }

    private static string FormatMs(double milliseconds) =>
        milliseconds.ToString("F0", CultureInfo.InvariantCulture) + "ms";

    private static string StripOrigin(string location) =>
        Uri.TryCreate(location, UriKind.Absolute, out var uri) ? uri.PathAndQuery : location;

    private static void Emit(string line)
    {
        try
        {
            if (Sink is { } sink)
                sink(line);
            else
                Console.WriteLine(Prefix + " " + line);
        }
        catch
        {
            // Diagnostics must never break the app.
        }
    }
}
