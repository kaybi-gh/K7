using Android.Views;
using K7.Clients.MAUI.Controls.Video;
using K7.Clients.Shared.Helpers;
using K7.Shared;
using Microsoft.Maui.ApplicationModel;
using System.Globalization;
using System.Text;

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// HDMI auto frame rate via window preferredDisplayModeId.
/// User setting: Disabled / SCALE_ON_DEVICE / SCALE_ON_TV. 23.976/24
/// content on a 60 Hz mode is 3:2 pulldown; some Amlogic boxes hitch more at 24 Hz.
/// </summary>
internal static class AndroidDisplayAfr
{
    private static int _savedModeId;
    private static bool _applied;
    private static bool _loggedModes;
    private static bool _vendorAfrOverridden;

    internal static HdmiAutoFrameRateMode ResolveMode()
    {
        var manufacturer = global::Android.OS.Build.Manufacturer ?? "";
        var model = global::Android.OS.Build.Model ?? "";
        var stored = "";
        try
        {
            stored = Microsoft.Maui.Storage.Preferences.Default.Get(
                PreferenceKeys.VIDEO_HDMI_AFR.Name,
                "");
        }
        catch
        {
        }

        return HdmiAutoFrameRatePolicy.Resolve(
            stored,
            AndroidExoHlsTuning.IsAndroidTelevision(),
            manufacturer,
            model);
    }

    internal static string PolicyHudLabel()
    {
        var mode = ResolveMode();
        if (mode == HdmiAutoFrameRateMode.Disabled)
            return "afr off";

        var kind = mode == HdmiAutoFrameRateMode.ScaleOnTv ? "tv" : "device";
        return IsApplied ? "afr " + kind + " window" : "afr " + kind + " idle";
    }

    internal static void Apply(
        float fps,
        int contentWidth,
        int contentHeight,
        bool preferContentResolution)
    {
        if (fps <= 1f)
            return;

        LogModesOnce();
        ApplyCore(fps, contentWidth, contentHeight, preferContentResolution);
    }

    internal static bool IsApplied => _applied;

    internal static void Restore()
    {
        if (_vendorAfrOverridden)
        {
            AndroidExoHlsTuning.TrySetVendorVideoAfrPolicy("2");
            _vendorAfrOverridden = false;
        }

        if (!_applied && _savedModeId == 0)
            return;

        if (_savedModeId != 0)
        {
            TrySetModeId(_savedModeId);
            WaitUntilModeId(_savedModeId, TimeSpan.FromMilliseconds(750));
            _savedModeId = 0;
        }

        _applied = false;
        _loggedModes = false;
    }

    internal static bool TryReadCurrentMode(out int width, out int height, out float hz)
    {
        width = 0;
        height = 0;
        hz = 0;
        if (!OperatingSystem.IsAndroidVersionAtLeast(30))
            return false;

        try
        {
            var display = Platform.CurrentActivity?.Display;
            var mode = display?.GetMode();
            if (mode is null)
                return false;

            width = mode.PhysicalWidth;
            height = mode.PhysicalHeight;
            hz = mode.RefreshRate;
            return hz > 1f;
        }
        catch
        {
            return false;
        }
    }

    internal static IReadOnlyList<HdmiDisplayMode> ListModes()
    {
        if (!TryGetModes(out var modes, out var current))
            return [];

        var currentId = current?.ModeId ?? 0;
        var list = new List<HdmiDisplayMode>(modes.Length);
        foreach (var mode in modes)
        {
            if (mode is null)
                continue;
            list.Add(new HdmiDisplayMode(
                mode.PhysicalWidth,
                mode.PhysicalHeight,
                mode.RefreshRate,
                mode.ModeId == currentId));
        }

        return list;
    }

    private static void ApplyCore(
        float fps,
        int contentWidth,
        int contentHeight,
        bool preferContentResolution)
    {
        if (!TryGetModes(out var modes, out var current))
            return;

        var manufacturer = global::Android.OS.Build.Manufacturer ?? "";
        var model = global::Android.OS.Build.Model ?? "";
        var currentId = current?.ModeId ?? 0;
        var best = PickMode(modes, fps, current, contentWidth, contentHeight, preferContentResolution);
        if (best is null)
            return;

        if (best.ModeId != currentId)
        {
            if (!_applied)
                _savedModeId = currentId;

            if (!TrySetModeId(best.ModeId))
                return;

            _applied = true;
            WaitUntilMode(best, TimeSpan.FromMilliseconds(750));
            NativeVideoDebug.Log(
                "HdmiAfr switch "
                + current!.PhysicalWidth + "x" + current.PhysicalHeight + "@"
                + current.RefreshRate.ToString("0.##", CultureInfo.InvariantCulture)
                + " -> "
                + best.PhysicalWidth + "x" + best.PhysicalHeight + "@"
                + best.RefreshRate.ToString("0.##", CultureInfo.InvariantCulture)
                + " content=" + contentWidth + "x" + contentHeight);
        }

        PinVendorHalAfr(manufacturer, model);
    }

    private static void PinVendorHalAfr(string manufacturer, string model)
    {
        if (_vendorAfrOverridden)
            return;
        if (!AndroidExoPlaybackPolicy.ShouldDisableVendorVideoAfr(manufacturer, model))
            return;

        AndroidExoHlsTuning.TrySetVendorVideoAfrPolicy("0");
        _vendorAfrOverridden = true;
        NativeVideoDebug.Log("HdmiAfr vendor policy=0");
    }

    private static bool TryGetModes(out Display.Mode[] modes, out Display.Mode? current)
    {
        modes = [];
        current = null;
        if (!OperatingSystem.IsAndroidVersionAtLeast(30))
            return false;

        try
        {
            var activity = Platform.CurrentActivity;
            var display = activity?.Display;
            var supported = display?.GetSupportedModes();
            if (activity?.Window is null || display is null || supported is null || supported.Length == 0)
                return false;

            modes = supported;
            current = display.GetMode();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void LogModesOnce()
    {
        if (_loggedModes)
            return;
        if (!TryGetModes(out var modes, out _))
            return;

        _loggedModes = true;
        var sb = new StringBuilder("HdmiModes");
        foreach (var mode in modes)
        {
            if (mode is null)
                continue;
            sb.Append(' ')
                .Append(mode.PhysicalWidth).Append('x').Append(mode.PhysicalHeight)
                .Append('@').Append(mode.RefreshRate.ToString("0.###", CultureInfo.InvariantCulture))
                .Append(" id=").Append(mode.ModeId);
        }

        NativeVideoDebug.Log(sb.ToString());
    }

    private static Display.Mode? PickMode(
        Display.Mode[] modes,
        float fps,
        Display.Mode? current,
        int contentWidth,
        int contentHeight,
        bool preferContentResolution)
    {
        var currentWidth = current?.PhysicalWidth ?? 0;
        var currentHeight = current?.PhysicalHeight ?? 0;
        Display.Mode? best = null;
        var bestScore = double.MaxValue;
        foreach (var mode in modes)
        {
            if (mode is null)
                continue;

            var score = AndroidHdmiFrameRateMatching.ModeScore(
                mode.RefreshRate,
                mode.PhysicalWidth,
                mode.PhysicalHeight,
                fps,
                currentWidth,
                currentHeight,
                contentWidth,
                contentHeight,
                preferContentResolution);
            if (score >= bestScore)
                continue;

            bestScore = score;
            best = mode;
        }

        if (best is null || current is null || bestScore >= double.MaxValue)
            return current;

        var currentScore = AndroidHdmiFrameRateMatching.ModeScore(
            current.RefreshRate,
            current.PhysicalWidth,
            current.PhysicalHeight,
            fps,
            currentWidth,
            currentHeight,
            contentWidth,
            contentHeight,
            preferContentResolution);
        return AndroidHdmiFrameRateMatching.ShouldSwitch(currentScore, bestScore)
            ? best
            : current;
    }

    private static void WaitUntilMode(Display.Mode target, TimeSpan timeout)
    {
        WaitUntilModeId(target.ModeId, timeout);
    }

    private static void WaitUntilModeId(int modeId, TimeSpan timeout)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(30))
            return;

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var current = Platform.CurrentActivity?.Display?.GetMode();
                if (current is not null && current.ModeId == modeId)
                    return;
            }
            catch
            {
            }

            Thread.Sleep(16);
        }
    }

    private static bool TrySetModeId(int modeId)
    {
        try
        {
            var window = Platform.CurrentActivity?.Window;
            if (window is null)
                return false;

            var attrs = window.Attributes;
            if (attrs is null)
                return false;

            attrs.PreferredDisplayModeId = modeId;
            window.Attributes = attrs;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
