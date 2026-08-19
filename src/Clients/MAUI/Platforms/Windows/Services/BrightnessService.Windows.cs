namespace K7.Clients.MAUI.Services;

public partial class BrightnessService
{
    public partial void SetBrightness(double brightness)
    {
        _brightness = Math.Clamp(brightness, 0, 1);
        _overridden = true;
    }

    public partial void ResetBrightness()
    {
        _overridden = false;
        _brightness = 1.0;
    }

    private partial double ReadCurrentBrightness() => _brightness;
}
