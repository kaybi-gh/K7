namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Dedicated TV-remote transport keys (fast-forward / rewind). Unlike D-pad left/right, these
/// always skip by Settings -&gt; Video playback durations, even when chrome is visible.
/// Android keyCode values match <c>android.view.KeyEvent</c> (no Android reference required).
/// </summary>
public static class VideoRemoteTransportKeys
{
    public const int AndroidMediaRewind = 89;
    public const int AndroidMediaFastForward = 90;
    public const int AndroidMediaSkipBackward = 273;
    public const int AndroidMediaSkipForward = 272;

    public const string OverlayFastForward = "mediafastforward";
    public const string OverlayRewind = "mediarewind";

    public static bool IsAndroidSkipForward(int keyCode) =>
        keyCode is AndroidMediaFastForward or AndroidMediaSkipForward;

    public static bool IsAndroidSkipBack(int keyCode) =>
        keyCode is AndroidMediaRewind or AndroidMediaSkipBackward;

    public static bool IsAndroidSkip(int keyCode) =>
        IsAndroidSkipForward(keyCode) || IsAndroidSkipBack(keyCode);

    public static string OverlayKey(bool forward) =>
        forward ? OverlayFastForward : OverlayRewind;

    public static bool IsOverlaySkipForward(string? key) =>
        key is OverlayFastForward or "mediaskipforward";

    public static bool IsOverlaySkipBack(string? key) =>
        key is OverlayRewind or "mediaskipbackward";

    public static bool IsOverlaySkip(string? key) =>
        IsOverlaySkipForward(key) || IsOverlaySkipBack(key);
}
