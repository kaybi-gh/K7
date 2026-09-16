using K7.Clients.Shared.Interfaces;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace K7.Clients.MAUI.Platforms.Windows;

public sealed class WindowsLocalPathPicker : ILocalPathPicker
{
    public bool CanPick => true;

    public Task<string?> PickFolderAsync(string? initialPath = null) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var hwnd = GetHwnd();
            if (hwnd == IntPtr.Zero)
                return null;

            var picker = new FolderPicker();
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            picker.FileTypeFilter.Add("*");

            var folder = await picker.PickSingleFolderAsync();
            return folder?.Path;
        });

    public Task<string?> PickFileAsync(IReadOnlyList<string> extensions, string? initialPath = null) =>
        MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var hwnd = GetHwnd();
            if (hwnd == IntPtr.Zero)
                return null;

            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            if (extensions.Count == 0)
            {
                picker.FileTypeFilter.Add("*");
            }
            else
            {
                foreach (var extension in extensions)
                {
                    var filter = extension.StartsWith('.') ? extension : "." + extension;
                    picker.FileTypeFilter.Add(filter);
                }
            }

            var file = await picker.PickSingleFileAsync();
            return file?.Path;
        });

    private static IntPtr GetHwnd()
    {
        var window = Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window native)
            return WindowNative.GetWindowHandle(native);

        return IntPtr.Zero;
    }
}
