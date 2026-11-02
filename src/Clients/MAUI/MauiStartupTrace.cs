using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
#if ANDROID
// No "using Android.OS": it makes Debug and Environment ambiguous with System.*.
using Android.Util;
#endif

namespace K7.Clients.MAUI;

/// <summary>
/// Monotonic cold-start markers for phone/TV Release (adb logcat / debug).
/// Filter: <c>adb logcat -s K7-Startup</c>.
/// Line format: <c>name +elapsedms jit=methods/ms asm=loadedAssemblies gc0=gen0Collections</c>.
/// The JIT counters are the Mono process totals: a jump between two markers shows whether
/// the time went into JIT (profiled AOT misses) or into something else (I/O, main thread
/// contention, WebView).
/// </summary>
internal static class MauiStartupTrace
{
    private const string Tag = "K7-Startup";
    private static readonly long StartTimestamp = Stopwatch.GetTimestamp();
    private static readonly object Gate = new();
    private static readonly HashSet<string> Seen = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> RepeatCounts = new(StringComparer.Ordinal);

    /// <summary>Logs the first occurrence of <paramref name="name"/> only.</summary>
    public static void Mark(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var ms = Stopwatch.GetElapsedTime(StartTimestamp).TotalMilliseconds;

        lock (Gate)
        {
            if (!Seen.Add(name))
                return;
        }

        Emit(name, ms);
    }

    /// <summary>
    /// Logs every occurrence as <c>name#n</c>. For events that legitimately repeat (token
    /// refreshes, restore attempts, storage reads): <see cref="Mark"/> would hide the repeats,
    /// which is how four extra refresh grants per cold start went unnoticed.
    /// </summary>
    public static void MarkRepeat(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var ms = Stopwatch.GetElapsedTime(StartTimestamp).TotalMilliseconds;
        int count;
        lock (Gate)
        {
            RepeatCounts.TryGetValue(name, out count);
            count++;
            RepeatCounts[name] = count;
        }

        Emit(name + "#" + count, ms);
    }

    /// <summary>Always-on error line: <c>adb logcat -s K7-Error</c>.</summary>
    public static void WriteError(string line)
    {
        Debug.WriteLine("K7-Error " + line);
#if ANDROID
        try
        {
            Log.Error("K7-Error", line);
        }
        catch
        {
            // Logging must never break the app.
        }
#else
        Console.Error.WriteLine("K7-Error " + line);
#endif
    }

    /// <summary>
    /// Sink for <see cref="K7.Clients.Shared.Services.PerfTrace"/> lines: <c>adb logcat -s K7-Perf</c>.
    /// </summary>
    public static void WritePerf(string line)
    {
        Debug.WriteLine("K7-Perf " + line);
#if ANDROID
        try
        {
            Log.Info("K7-Perf", line);
        }
        catch
        {
            // Logging must never break the app.
        }
#else
        Console.WriteLine("K7-Perf " + line);
#endif
    }

    private static void Emit(string label, double ms)
    {
        var line = label + " +" + ms.ToString("F0") + "ms" + RuntimeCounters();

        Debug.WriteLine(Tag + " " + line);
#if ANDROID
        try
        {
            Log.Info(Tag, line);
        }
        catch
        {
            // Logging must never break startup.
        }
#endif
    }

    /// <summary>One-shot process facts (cores, memory, runtime) next to the first marker.</summary>
    public static void MarkEnvironment()
    {
        try
        {
            var memoryMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024 * 1024);
            Mark("env cpu=" + Environment.ProcessorCount
                + " mem=" + memoryMb + "MB"
                + " arch=" + RuntimeInformation.ProcessArchitecture
                + " runtime=" + RuntimeInformation.FrameworkDescription);
        }
        catch
        {
            // Diagnostics only.
        }
    }

    public static void ResetForTests()
    {
        lock (Gate)
        {
            Seen.Clear();
            RepeatCounts.Clear();
        }
    }

    private static string RuntimeCounters()
    {
        try
        {
            var jitMethods = JitInfo.GetCompiledMethodCount();
            var jitMs = JitInfo.GetCompilationTime().TotalMilliseconds;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Length;
            return " jit=" + jitMethods + "/" + jitMs.ToString("F0") + "ms"
                + " asm=" + assemblies
                + " gc0=" + GC.CollectionCount(0);
        }
        catch
        {
            return string.Empty;
        }
    }

#if ANDROID
    private const int WatchdogIntervalMs = 100;
    private const int WatchdogStallThresholdMs = 250;
    private static readonly TimeSpan WatchdogDuration = TimeSpan.FromSeconds(90);
    private static int _watchdogStarted;

    /// <summary>
    /// Phase 0 diagnostics. Posts to the main looper every 100ms for 90s and logs
    /// <c>MainThread-stall#n Xms</c> whenever the post waited 250ms or more. The phase
    /// markers alone cannot tell a busy UI thread from a slow WebView.
    /// </summary>
    public static void StartMainThreadWatchdog()
    {
        if (Interlocked.Exchange(ref _watchdogStarted, 1) != 0)
            return;

        var thread = new Thread(RunMainThreadWatchdog)
        {
            IsBackground = true,
            Name = "k7-startup-watchdog"
        };
        thread.Start();
    }

    private static void RunMainThreadWatchdog()
    {
        try
        {
            var handler = new global::Android.OS.Handler(global::Android.OS.Looper.MainLooper!);
            var stalls = 0;
            var deadline = Stopwatch.GetTimestamp() + (long)(WatchdogDuration.TotalSeconds * Stopwatch.Frequency);
            while (Stopwatch.GetTimestamp() < deadline)
            {
                using var done = new ManualResetEventSlim(false);
                var posted = Stopwatch.GetTimestamp();
                if (!handler.Post(done.Set))
                    return;

                done.Wait();
                var waitedMs = Stopwatch.GetElapsedTime(posted).TotalMilliseconds;
                if (waitedMs >= WatchdogStallThresholdMs)
                {
                    stalls++;
                    Mark("MainThread-stall#" + stalls + " " + waitedMs.ToString("F0") + "ms");
                }

                Thread.Sleep(WatchdogIntervalMs);
            }

            Mark("MainThread-watchdog-end stalls=" + stalls);
        }
        catch
        {
            // Diagnostics only.
        }
    }
#endif
}
