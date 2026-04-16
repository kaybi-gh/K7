using K7.Clients.Shared.Interfaces;

namespace K7.Clients.MAUI.Services;

public partial class BrightnessService : IBrightnessService
{
    private double _brightness = 1.0;

#pragma warning disable CS0414
    private float _originalBrightness = -1f;
#pragma warning restore CS0414

    public double Brightness => _brightness;

    public partial void SetBrightness(double brightness);

    public partial void ResetBrightness();
}
