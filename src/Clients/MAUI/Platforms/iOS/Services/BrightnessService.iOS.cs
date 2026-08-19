using UIKit;

namespace K7.Clients.MAUI.Services;

public partial class BrightnessService
{
    public partial void SetBrightness(double brightness)
    {
        _brightness = Math.Clamp(brightness, 0, 1);

        if (!_overridden)
        {
            _originalBrightness = (float)UIScreen.MainScreen.Brightness;
            _overridden = true;
        }

        UIScreen.MainScreen.Brightness = (nfloat)_brightness;
    }

    public partial void ResetBrightness()
    {
        if (_overridden)
        {
            UIScreen.MainScreen.Brightness = (nfloat)_originalBrightness;
            _overridden = false;
            _originalBrightness = -1f;
        }
    }

    private partial double ReadCurrentBrightness() => UIScreen.MainScreen.Brightness;
}
