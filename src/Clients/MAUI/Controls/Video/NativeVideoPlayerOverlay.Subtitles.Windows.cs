#if WINDOWS
using K7.Clients.MAUI.Playback;
using K7.Clients.Shared.Helpers;
using K7.Shared.Dtos;
using WinBorder = Microsoft.UI.Xaml.Controls.Border;
using WinBrush = Microsoft.UI.Xaml.Media.SolidColorBrush;
using WinColor = Windows.UI.Color;
using WinFrameworkElement = Microsoft.UI.Xaml.FrameworkElement;
using WinHorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment;
using WinPopup = Microsoft.UI.Xaml.Controls.Primitives.Popup;
using WinTextAlignment = Microsoft.UI.Xaml.TextAlignment;
using WinTextBlock = Microsoft.UI.Xaml.Controls.TextBlock;
using WinTextWrapping = Microsoft.UI.Xaml.TextWrapping;
using WinThickness = Microsoft.UI.Xaml.Thickness;

namespace K7.Clients.MAUI.Controls.Video;

public sealed partial class NativeVideoPlayerOverlay
{
    private WinPopup? _windowsSubtitlePopup;
    private WinTextBlock? _windowsSubtitleText;
    private WinBorder? _windowsSubtitleBorder;
    private bool _windowsSubtitleHooks;
    private bool _windowsSubtitlePopupMissLogged;

    internal bool IsWindowsSubtitlePopupLive() =>
        _windowsSubtitlePopup is { IsOpen: true } && _windowsSubtitleText is not null;

    private void EnsureWindowsSubtitlePopup()
    {
        HookWindowsSubtitleHost();
        if (_windowsSubtitlePopup is not null)
        {
            SyncWindowsSubtitleXamlRoot();
            return;
        }

        var root = TryGetWindowsXamlRoot();
        if (root is null)
        {
            if (!_windowsSubtitlePopupMissLogged)
            {
                _windowsSubtitlePopupMissLogged = true;
                VlcPlayerLog.Warn("sidecar windows popup miss xamlroot");
            }

            return;
        }

        _windowsSubtitleText = new WinTextBlock
        {
            TextWrapping = WinTextWrapping.Wrap,
            TextAlignment = WinTextAlignment.Center,
            IsHitTestVisible = false
        };
        _windowsSubtitleBorder = new WinBorder
        {
            Child = _windowsSubtitleText,
            Padding = new WinThickness(12, 6, 12, 6),
            HorizontalAlignment = WinHorizontalAlignment.Center,
            IsHitTestVisible = false
        };
        _windowsSubtitlePopup = new WinPopup
        {
            Child = _windowsSubtitleBorder,
            IsHitTestVisible = false,
            ShouldConstrainToRootBounds = false,
            XamlRoot = root
        };
        ApplyWindowsSubtitlePopupStyle();
        VlcPlayerLog.Info("sidecar windows popup");
    }

    private void HookWindowsSubtitleHost()
    {
        if (_windowsSubtitleHooks)
            return;

        _windowsSubtitleHooks = true;
        HandlerChanged += (_, _) =>
        {
            EnsureWindowsSubtitlePopup();
            PositionWindowsSubtitlePopup();
        };
        SizeChanged += (_, _) => PositionWindowsSubtitlePopup();
    }

    private void SyncWindowsSubtitleXamlRoot()
    {
        var root = TryGetWindowsXamlRoot();
        if (root is null || _windowsSubtitlePopup is null)
            return;

        if (!ReferenceEquals(_windowsSubtitlePopup.XamlRoot, root))
            _windowsSubtitlePopup.XamlRoot = root;
    }

    private Microsoft.UI.Xaml.XamlRoot? TryGetWindowsXamlRoot()
    {
        if (Handler?.PlatformView is WinFrameworkElement local && local.XamlRoot is not null)
            return local.XamlRoot;

        var window = Application.Current?.Windows.FirstOrDefault();
        if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window native
            && native.Content is WinFrameworkElement content
            && content.XamlRoot is not null)
        {
            return content.XamlRoot;
        }

        return null;
    }

    private void UpdateWindowsSubtitlePopup(string? text)
    {
        EnsureWindowsSubtitlePopup();
        if (_windowsSubtitlePopup is null || _windowsSubtitleText is null)
            return;

        SyncWindowsSubtitleXamlRoot();
        var show = !string.IsNullOrEmpty(text);
        var next = text ?? string.Empty;
        if (!show)
        {
            _windowsSubtitleText.Text = string.Empty;
            _windowsSubtitlePopup.IsOpen = false;
            return;
        }

        var changed = _windowsSubtitleText.Text != next || !_windowsSubtitlePopup.IsOpen;
        _windowsSubtitleText.Text = next;
        if (changed && _windowsSubtitlePopup.IsOpen)
            _windowsSubtitlePopup.IsOpen = false;

        _windowsSubtitlePopup.IsOpen = true;
        _windowsSubtitleBorder?.InvalidateMeasure();
        _windowsSubtitleBorder?.InvalidateArrange();
        PositionWindowsSubtitlePopup();
    }

    private void PositionWindowsSubtitlePopup()
    {
        if (_windowsSubtitlePopup is not { IsOpen: true }
            || _windowsSubtitleBorder is null)
            return;

        var host = Handler?.PlatformView as WinFrameworkElement;
        if (host is null || host.ActualWidth <= 0 || host.ActualHeight <= 0)
        {
            var window = Application.Current?.Windows.FirstOrDefault();
            if (window?.Handler?.PlatformView is Microsoft.UI.Xaml.Window native
                && native.Content is WinFrameworkElement content
                && content.ActualWidth > 0)
                host = content;
            else
                return;
        }

        var maxWidth = Math.Max(80, host.ActualWidth - 96);
        _windowsSubtitleBorder.MaxWidth = maxWidth;
        _windowsSubtitleBorder.Measure(new Windows.Foundation.Size(maxWidth, double.PositiveInfinity));
        var width = _windowsSubtitleBorder.DesiredSize.Width;
        var height = _windowsSubtitleBorder.DesiredSize.Height;
        _windowsSubtitlePopup.HorizontalOffset = Math.Max(0, (host.ActualWidth - width) / 2);
        _windowsSubtitlePopup.VerticalOffset = Math.Max(0, host.ActualHeight - height - 36);
    }

    private void ApplyWindowsSubtitlePopupStyle()
    {
        if (_windowsSubtitleText is null || _windowsSubtitleBorder is null)
            return;

        var settings = _videoSettings ?? _player.VideoPlayerUxSettings ?? new VideoPlayerSettingsDto();
        var size = SubtitleStyleHelper.ToFontSizePx(settings.SubtitleFontSize, _deviceType);
        _windowsSubtitleText.FontSize = size;

        if (SubtitleStyleHelper.TryParseHexColor(settings.SubtitleFontColor, out var a, out var r, out var g, out var b))
            _windowsSubtitleText.Foreground = new WinBrush(WinColor.FromArgb(a, r, g, b));
        else
            _windowsSubtitleText.Foreground = new WinBrush(WinColor.FromArgb(255, 255, 255, 255));

        var bg = Math.Clamp(settings.SubtitleBackgroundOpacity, 0, 1);
        _windowsSubtitleBorder.Background = bg <= 0.01
            ? new WinBrush(WinColor.FromArgb(0, 0, 0, 0))
            : new WinBrush(WinColor.FromArgb((byte)Math.Clamp(bg * 255.0, 0, 255), 0, 0, 0));
    }

    private void CloseWindowsSubtitlePopup()
    {
        if (_windowsSubtitlePopup is null)
            return;

        _windowsSubtitlePopup.IsOpen = false;
    }
}
#endif
