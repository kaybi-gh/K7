namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Pure D-pad focus math for the native MAUI playback settings panel (no wrap-around,
/// clamps at the list edges - matches typical TV remote list navigation).
/// </summary>
public static class NativeSettingsFocusNavigator
{
    /// <summary>
    /// Returns the next focused row index for a D-pad up (<paramref name="direction"/> &lt; 0)
    /// or down (<paramref name="direction"/> &gt; 0) press. Clamps to [0, itemCount - 1].
    /// Returns -1 when the list is empty.
    /// </summary>
    public static int MoveFocus(int currentIndex, int itemCount, int direction)
    {
        if (itemCount <= 0)
            return -1;

        if (currentIndex < 0)
            return direction >= 0 ? 0 : itemCount - 1;

        var next = currentIndex + Math.Sign(direction);
        return Math.Clamp(next, 0, itemCount - 1);
    }

    /// <summary>Clamps a focus index into range after the row list changes (e.g. Rebuild()).</summary>
    public static int ClampFocus(int currentIndex, int itemCount)
    {
        if (itemCount <= 0)
            return -1;

        return Math.Clamp(currentIndex, 0, itemCount - 1);
    }
}
