using Android.Views;
using Microsoft.Maui.ApplicationModel;

namespace K7.Clients.MAUI.Platforms.Android;

/// <summary>
/// HDMI auto frame rate. 23.976/24 content on a 60 Hz mode looks like micro-freezes
/// (3:2 pulldown) even when the decoder is on time.
/// </summary>
internal static class AndroidDisplayAfr
{
    private static int _savedModeId;
    private static bool _applied;

    internal static void Apply(float fps)
    {
        if (fps <= 1f)
            return;

        ApplyCore(fps);
    }

    internal static void Restore()
    {
        if (!_applied)
            return;

        TrySetModeId(_savedModeId);
        _applied = false;
    }

    private static void ApplyCore(float fps)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(30))
            return;

        var activity = Platform.CurrentActivity;
        var display = activity?.Display;
        var modes = display?.GetSupportedModes();
        if (activity?.Window is null || display is null || modes is null || modes.Length == 0)
            return;

        var current = display.GetMode();
        var currentId = current?.ModeId ?? 0;
        var best = PickMode(modes, fps, current);
        if (best is null || best.ModeId == currentId)
            return;

        if (!_applied)
            _savedModeId = currentId;

        if (!TrySetModeId(best.ModeId))
            return;

        _applied = true;
    }

    private static Display.Mode? PickMode(Display.Mode[] modes, float fps, Display.Mode? current)
    {
        Display.Mode? best = null;
        var bestScore = double.MaxValue;
        foreach (var mode in modes)
        {
            if (mode is null)
                continue;

            var score = Score(mode.RefreshRate, fps);
            if (score < bestScore)
            {
                bestScore = score;
                best = mode;
            }
        }

        if (best is null || current is null)
            return best;

        var currentScore = Score(current.RefreshRate, fps);
        // Only switch when the new mode is clearly closer (avoid 59.94 <-> 60 flicker).
        return currentScore - bestScore >= 0.4 ? best : current;
    }

    private static double Score(float hz, float fps)
    {
        var direct = Math.Abs(hz - fps);
        var doubleRate = Math.Abs(hz - (fps * 2f));
        return Math.Min(direct, doubleRate + 0.15);
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
