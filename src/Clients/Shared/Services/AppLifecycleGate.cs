namespace K7.Clients.Shared.Services;

/// <summary>
/// Foreground/background gate for Blazor Hybrid. While the host is paused (phone sleep,
/// TV HDMI off), WebView JS is frozen but C# events keep firing. Queued render batches
/// then replay on resume as a track-by-track catch-up in the music UI.
/// </summary>
public static class AppLifecycleGate
{
    public static event Action? ForegroundChanged;

    public static bool IsForeground { get; private set; } = true;

    public static void SetForeground(bool foreground)
    {
        if (IsForeground == foreground)
            return;

        IsForeground = foreground;
        ForegroundChanged?.Invoke();
    }
}
