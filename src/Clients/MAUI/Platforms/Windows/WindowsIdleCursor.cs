#if WINDOWS
using System.Runtime.InteropServices;

namespace K7.Clients.MAUI.Platforms.Windows;

/// <summary>
/// Hide the mouse cursor after idle in fullscreen (WinUI has no none-cursor shape).
/// <c>ShowCursor</c> is reference-counted.
/// </summary>
internal static class WindowsIdleCursor
{
    private static bool _hidden;

    public static void Hide()
    {
        if (_hidden)
            return;

        while (ShowCursor(false) >= 0)
        {
        }

        _hidden = true;
    }

    public static void Show()
    {
        if (!_hidden)
            return;

        while (ShowCursor(true) < 0)
        {
        }

        _hidden = false;
    }

    [DllImport("user32.dll")]
    private static extern int ShowCursor(bool bShow);
}
#endif
