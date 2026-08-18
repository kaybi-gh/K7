namespace K7.Clients.MAUI;

/// <summary>
/// First MAUI frame. Android keeps the system splash until this is set so the
/// user is not staring at a blank window during CreateMauiApp / BlazorPage ctor.
/// </summary>
internal static class MauiStartupVisual
{
    public static bool IsFirstFrameReady { get; private set; }

    public static event Action? FirstFrameReady;
    public static event Action? StartPageSet;

    public static void NotifyFirstFrame()
    {
        if (IsFirstFrameReady)
            return;

        IsFirstFrameReady = true;
        FirstFrameReady?.Invoke();
    }

    public static void NotifyStartPageSet() => StartPageSet?.Invoke();

    public static void Reset() => IsFirstFrameReady = false;
}
