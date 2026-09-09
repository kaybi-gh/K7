using System.Net.Http.Json;
using K7.Shared.Interfaces;

namespace K7.Clients.MAUI.Services.Authentication;

/// <summary>
/// Breadcrumbs for native interactive login. Writes %TEMP%\k7-auth.log and posts
/// the same stage to the server so docker logs show where the client stopped.
/// </summary>
internal static class NativeAuthTrace
{
    private static readonly object FileGate = new();
    private static string? _tempLogPath;
    private static string? _appLogPath;
    private static IServiceProvider? _services;
    private static int _installed;

    public static string? TempLogPath => _tempLogPath;

    public static void Install()
    {
        if (Interlocked.Exchange(ref _installed, 1) != 0)
            return;

        try
        {
            _tempLogPath = Path.Combine(Path.GetTempPath(), "k7-auth.log");
            WriteLine($"K7 auth trace started {DateTime.Now:O} temp={_tempLogPath}");
        }
        catch
        {
        }

        try
        {
            _appLogPath = Path.Combine(FileSystem.AppDataDirectory, "k7-auth.log");
            WriteLine($"appdata={_appLogPath}");
        }
        catch
        {
        }
    }

    public static void Configure(IServiceProvider services) => _services = services;

    public static void Write(string stage, string? detail = null)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {stage} {Sanitize(detail)}";
        System.Diagnostics.Debug.WriteLine("K7 AUTH " + line);
        WriteLine(line);
        _ = PostToServerAsync(stage, detail);
    }

    private static void WriteLine(string line)
    {
        lock (FileGate)
        {
            TryAppend(_tempLogPath, line);
            TryAppend(_appLogPath, line);
        }
    }

    private static void TryAppend(string? path, string line)
    {
        if (path is null)
            return;

        try
        {
            File.AppendAllText(path, line + Environment.NewLine);
        }
        catch
        {
        }
    }

    private static async Task PostToServerAsync(string stage, string? detail)
    {
        try
        {
            var services = _services ?? IPlatformApplication.Current?.Services;
            var server = services?.GetService<IK7ServerService>();
            var baseAddress = server?.HttpClient.BaseAddress;
            if (baseAddress is null)
                return;

            var deviceId = services?.GetService<K7.Clients.Shared.Interfaces.IDeviceService>()?.GetDeviceId();
            using var client = new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(3) };
            await client.PostAsJsonAsync("/api/diagnostics/auth-trace", new
            {
                stage,
                detail = Sanitize(detail),
                deviceId
            }).ConfigureAwait(false);
        }
        catch
        {
        }
    }

    private static string Sanitize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "-";

        var trimmed = value.Trim().Replace('\r', ' ').Replace('\n', ' ');
        var queryIndex = trimmed.IndexOf('?');
        if (queryIndex >= 0)
            trimmed = trimmed[..queryIndex];

        return trimmed.Length <= 200 ? trimmed : trimmed[..200];
    }
}
