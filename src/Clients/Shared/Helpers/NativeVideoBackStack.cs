using K7.Server.Domain.Enums;

namespace K7.Clients.Shared.Helpers;

public enum NativeVideoBackAction
{
    NotHandled,
    Consumed,
    HidePlayerAsync,
    ClosePlayer
}

public readonly struct NativeVideoBackContext
{
    public bool SettingsHandledBack { get; init; }
    public bool VolumeOpen { get; init; }
    public bool SeekScrubbing { get; init; }
    public bool SeekBarDragging { get; init; }
    public bool ShowChrome { get; init; }
    public DateTime UtcNow { get; init; }
    public DateTime SuppressCloseUntil { get; init; }
    public PlaybackState PlaybackState { get; init; }
}

/// <summary>
/// Back-key priority for native MAUI video chrome (settings, volume, seek, chrome, exit).
/// </summary>
public static class NativeVideoBackStack
{
    public static (NativeVideoBackAction Action, bool CancelSeek, bool HideChrome, bool CloseVolume) Evaluate(
        NativeVideoBackContext context)
    {
        if (context.SettingsHandledBack)
            return (NativeVideoBackAction.Consumed, false, false, false);

        if (context.VolumeOpen)
            return (NativeVideoBackAction.Consumed, false, false, true);

        if (context.SeekScrubbing || context.SeekBarDragging)
            return (NativeVideoBackAction.Consumed, true, false, false);

        if (context.ShowChrome)
            return (NativeVideoBackAction.Consumed, false, true, false);

        if (context.UtcNow < context.SuppressCloseUntil)
            return (NativeVideoBackAction.Consumed, false, false, false);

        if (context.PlaybackState is PlaybackState.Idle or PlaybackState.Ended or PlaybackState.Unknown)
            return (NativeVideoBackAction.HidePlayerAsync, false, false, false);

        return (NativeVideoBackAction.ClosePlayer, false, false, false);
    }
}
