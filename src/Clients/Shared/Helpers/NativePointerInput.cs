namespace K7.Clients.Shared.Helpers;

/// <summary>
/// PointerGestureRecognizer on a parent or Button eats Android/iOS taps
/// (Clicked / TapGesture never fire). Hover is a Windows cursor concern only.
/// </summary>
public static class NativePointerInput
{
    public static bool SupportsHoverRecognizers =>
        ForPlatform(
            isWindows: OperatingSystem.IsWindows(),
            isAndroid: OperatingSystem.IsAndroid(),
            isIos: OperatingSystem.IsIOS());

    public static bool ForPlatform(bool isWindows, bool isAndroid, bool isIos)
    {
        if (isAndroid || isIos)
            return false;

        return isWindows;
    }
}
