using UIKit;

namespace K7.Clients.MAUI.Services;

public partial class BrightnessService
{
    public partial void SetBrightness(double brightness)
    {
        _brightness = Math.Clamp(brightness, 0, 1);

        if (_originalBrightness < 0)
        {
            _originalBrightness = (float)UIScreen.MainScreen.Brightness;
        }

        UIScreen.MainScreen.Brightness = (nfloat)_brightness;
    }

    public partial void ResetBrightness()
    {
        _brightness = 1.0;

        if (_originalBrightness >= 0)
        {
            UIScreen.MainScreen.Brightness = (nfloat)_originalBrightness;
            _originalBrightness = -1f;
        }
    }
}
