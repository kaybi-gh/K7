using K7.Clients.Shared.Interfaces;

namespace K7.Clients.Web.Services;

public class BrightnessService : IBrightnessService
{
    public double Brightness { get; private set; } = 1.0;

    public void SetBrightness(double brightness)
    {
        Brightness = Math.Clamp(brightness, 0, 1);
    }

    public void ResetBrightness()
    {
        Brightness = 1.0;
    }
}
