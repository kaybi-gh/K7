namespace K7.Clients.MAUI.Services;

public partial class BrightnessService
{
    public partial void SetBrightness(double brightness)
    {
        _brightness = Math.Clamp(brightness, 0, 1);
        // Windows desktop: no screen brightness API available
    }

    public partial void ResetBrightness()
    {
        _brightness = 1.0;
    }
}
