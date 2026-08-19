using Android.Provider;
using Android.Views;

namespace K7.Clients.MAUI.Services;

public partial class BrightnessService
{
    public partial void SetBrightness(double brightness)
    {
        _brightness = Math.Clamp(brightness, 0, 1);

        var activity = Platform.CurrentActivity;
        if (activity?.Window?.Attributes is null) return;

        if (!_overridden)
        {
            _originalBrightness = activity.Window.Attributes.ScreenBrightness;
            _overridden = true;
        }

        var lp = activity.Window.Attributes;
        // 0 is BRIGHTNESS_OVERRIDE_OFF and can blank the display. 1/255 is the
        // darkest window override that still keeps the screen on.
        lp.ScreenBrightness = Math.Max((float)_brightness, 1f / 255f);
        activity.Window.Attributes = lp;
    }

    public partial void ResetBrightness()
    {
        var activity = Platform.CurrentActivity;
        if (activity?.Window?.Attributes is not null && _overridden)
        {
            var lp = activity.Window.Attributes;
            lp.ScreenBrightness = _originalBrightness >= 0
                ? _originalBrightness
                : WindowManagerLayoutParams.BrightnessOverrideNone;
            activity.Window.Attributes = lp;
        }

        _overridden = false;
        _originalBrightness = -1f;
    }

    private partial double ReadCurrentBrightness()
    {
        var activity = Platform.CurrentActivity;
        if (activity?.Window?.Attributes is not null)
        {
            var windowBrightness = activity.Window.Attributes.ScreenBrightness;
            if (windowBrightness >= 0)
                return windowBrightness;
        }

        var resolver = activity?.ContentResolver;
        if (resolver is null)
            return _brightness;

        var raw = Settings.System.GetInt(resolver, Settings.System.ScreenBrightness, 128);
        return Math.Clamp(raw / 255.0, 0, 1);
    }
}
