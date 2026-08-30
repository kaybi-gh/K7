using System.Diagnostics;
using System.Runtime.InteropServices;
using K7.Shared;
using Microsoft.UI.Windowing;
using WinRT.Interop;
using WinUIWindow = Microsoft.UI.Xaml.Window;

namespace K7.Clients.MAUI.Platforms.Windows;

/// <summary>
/// Persists main window size, position, and maximized state across Windows launches.
/// Uses GetWindowPlacement/SetWindowPlacement so bounds match what the user set, and
/// swallows WM_DPICHANGED during restore so WinUI does not rescale the window.
/// </summary>
internal static class WindowGeometryPersistence
{
    private const int DefaultWidth = 1280;
    private const int DefaultHeight = 800;
    private const nuint SubclassId = 1;
    private const uint WmDpiChanged = 0x02E0;
    private const uint WmShowWindow = 0x0018;
    private const int SwShownormal = 1;
    private const int SwShowMinimized = 2;
    private const int SwShowMaximized = 3;
    private const int WpfRestoreToMaximized = 0x0002;
    private static readonly TimeSpan SaveDebounce = TimeSpan.FromMilliseconds(300);

    // Keep the delegate alive for the lifetime of the subclass.
    private static readonly SubclassProc SubclassCallback = OnSubclassMessage;

    private static IntPtr _hwnd;
    private static bool _restored;
    private static bool _restoring;
    private static CancellationTokenSource? _saveCts;

    public static void SetFullscreen(bool fullscreen)
    {
        var appWindow = GetAppWindow(_hwnd);
        if (appWindow is null)
            return;

        appWindow.SetPresenter(
            fullscreen ? AppWindowPresenterKind.FullScreen : AppWindowPresenterKind.Default);
    }

    public static void Attach(WinUIWindow nativeWindow)
    {
        var hwnd = WindowNative.GetWindowHandle(nativeWindow);
        if (hwnd == IntPtr.Zero)
            return;

        _hwnd = hwnd;
        _restored = false;

        if (!SetWindowSubclass(hwnd, SubclassCallback, SubclassId, UIntPtr.Zero))
            Debug.WriteLine("K7 MAUI - WindowGeometry SetWindowSubclass failed");

        var appWindow = GetAppWindow(hwnd);
        if (appWindow is not null)
            appWindow.Changed += OnAppWindowChanged;

        nativeWindow.Closed += (_, _) =>
        {
            if (appWindow is not null)
                appWindow.Changed -= OnAppWindowChanged;

            _saveCts?.Cancel();
            Save(hwnd);
            RemoveWindowSubclass(hwnd, SubclassCallback, SubclassId);
            _hwnd = IntPtr.Zero;
        };
    }

    private static IntPtr OnSubclassMessage(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        nuint uIdSubclass,
        nuint dwRefData)
    {
        // WinUI rescales on DPI change after SetWindowPlacement; that makes the
        // restored size drift from what the user set. Swallow during restore only.
        if (msg == WmDpiChanged && _restoring)
            return IntPtr.Zero;

        if (msg == WmShowWindow && wParam == (IntPtr)1 && !_restored)
        {
            _restored = true;
            _restoring = true;
            try
            {
                RestoreCore(hWnd);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"K7 MAUI - WindowGeometry Restore failed: {ex}");
                _restoring = false;
                return DefSubclassProc(hWnd, msg, wParam, lParam);
            }

            // MAUI / WinUI may nudge size right after first show; re-apply once.
            _ = ReapplyAfterSettleAsync(hWnd);
        }

        return DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    private static async Task ReapplyAfterSettleAsync(IntPtr hwnd)
    {
        try
        {
            await Task.Delay(150);
            RestoreCore(hwnd);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"K7 MAUI - WindowGeometry re-apply failed: {ex}");
        }
        finally
        {
            _restoring = false;
        }
    }

    private static void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (_restoring || !_restored)
            return;

        if (sender.Presenter.Kind == AppWindowPresenterKind.FullScreen)
            return;

        if (!args.DidPositionChange && !args.DidSizeChange && !args.DidPresenterChange)
            return;

        ScheduleSave(_hwnd);
    }

    private static void ScheduleSave(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero)
            return;

        _saveCts?.Cancel();
        _saveCts?.Dispose();
        _saveCts = new CancellationTokenSource();
        var token = _saveCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(SaveDebounce, token);
                Save(hwnd);
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private static AppWindow? GetAppWindow(IntPtr hwnd)
    {
        try
        {
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            return AppWindow.GetFromWindowId(windowId);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"K7 MAUI - WindowGeometry GetAppWindow failed: {ex}");
            return null;
        }
    }

    private static void RestoreCore(IntPtr hwnd)
    {
        if (!Preferences.Default.ContainsKey(PreferenceKeys.WINDOW_WIDTH.Name)
            || !Preferences.Default.ContainsKey(PreferenceKeys.WINDOW_HEIGHT.Name))
        {
            return;
        }

        var width = Preferences.Default.Get(PreferenceKeys.WINDOW_WIDTH.Name, DefaultWidth);
        var height = Preferences.Default.Get(PreferenceKeys.WINDOW_HEIGHT.Name, DefaultHeight);
        var x = Preferences.Default.Get(PreferenceKeys.WINDOW_X.Name, 0);
        var y = Preferences.Default.Get(PreferenceKeys.WINDOW_Y.Name, 0);
        var maximized = Preferences.Default.Get(PreferenceKeys.WINDOW_MAXIMIZED.Name, false);

        if (width < 1 || height < 1)
            return;

        var placement = new WindowPlacement
        {
            Length = Marshal.SizeOf<WindowPlacement>(),
            Flags = maximized ? WpfRestoreToMaximized : 0,
            ShowCmd = maximized ? SwShowMaximized : SwShownormal,
            NormalPosition = new Rect
            {
                Left = x,
                Top = y,
                Right = x + width,
                Bottom = y + height
            }
        };

        if (!SetWindowPlacement(hwnd, ref placement))
            Debug.WriteLine($"K7 MAUI - WindowGeometry SetWindowPlacement failed: {Marshal.GetLastWin32Error()}");
    }

    private static void Save(IntPtr hwnd)
    {
        if (hwnd == IntPtr.Zero || _restoring)
            return;

        try
        {
            var placement = new WindowPlacement
            {
                Length = Marshal.SizeOf<WindowPlacement>()
            };

            if (!GetWindowPlacement(hwnd, ref placement))
                return;

            if (placement.ShowCmd == SwShowMinimized)
                return;

            var maximized = placement.ShowCmd == SwShowMaximized
                || (placement.Flags & WpfRestoreToMaximized) == WpfRestoreToMaximized;

            var width = placement.NormalPosition.Right - placement.NormalPosition.Left;
            var height = placement.NormalPosition.Bottom - placement.NormalPosition.Top;
            if (width < 1 || height < 1)
                return;

            Preferences.Default.Set(PreferenceKeys.WINDOW_MAXIMIZED.Name, maximized);
            Preferences.Default.Set(PreferenceKeys.WINDOW_X.Name, placement.NormalPosition.Left);
            Preferences.Default.Set(PreferenceKeys.WINDOW_Y.Name, placement.NormalPosition.Top);
            Preferences.Default.Set(PreferenceKeys.WINDOW_WIDTH.Name, width);
            Preferences.Default.Set(PreferenceKeys.WINDOW_HEIGHT.Name, height);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"K7 MAUI - WindowGeometry Save failed: {ex}");
        }
    }

    private delegate IntPtr SubclassProc(
        IntPtr hWnd,
        uint msg,
        IntPtr wParam,
        IntPtr lParam,
        nuint uIdSubclass,
        nuint dwRefData);

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowPlacement
    {
        public int Length;
        public int Flags;
        public int ShowCmd;
        public Point MinPosition;
        public Point MaxPosition;
        public Rect NormalPosition;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPlacement(IntPtr hWnd, ref WindowPlacement lpwndpl);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        nuint uIdSubclass,
        nuint dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveWindowSubclass(
        IntPtr hWnd,
        SubclassProc pfnSubclass,
        nuint uIdSubclass);

    [DllImport("comctl32.dll", SetLastError = true)]
    private static extern IntPtr DefSubclassProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
