using Android.Views;

namespace K7.Clients.MAUI.Services;

public partial class BrightnessService
{
    public partial void SetBrightness(double brightness)
    {
        _brightness = Math.Clamp(brightness, 0, 1);

        var activity = Platform.CurrentActivity;
        if (activity?.Window?.Attributes is null) return;

        if (_originalBrightness < 0)
        {
            _originalBrightness = activity.Window.Attributes.ScreenBrightness;
        }

        var lp = activity.Window.Attributes;
        lp.ScreenBrightness = (float)_brightness;
        activity.Window.Attributes = lp;
    }

    public partial void ResetBrightness()
    {
        _brightness = 1.0;

        var activity = Platform.CurrentActivity;
        if (activity?.Window?.Attributes is null) return;

        var lp = activity.Window.Attributes;
        lp.ScreenBrightness = _originalBrightness >= 0
            ? _originalBrightness
            : WindowManagerLayoutParams.BrightnessOverrideNone;
        activity.Window.Attributes = lp;

        _originalBrightness = -1f;
    }
}
