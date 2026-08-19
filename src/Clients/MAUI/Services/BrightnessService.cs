using K7.Clients.Shared.Interfaces;

namespace K7.Clients.MAUI.Services;

public partial class BrightnessService : IBrightnessService
{
    private double _brightness = 1.0;
    private bool _overridden;

#pragma warning disable CS0414
    private float _originalBrightness = -1f;
#pragma warning restore CS0414

    public bool SupportsNativeBrightness => true;

    public double Brightness => _overridden ? _brightness : ReadCurrentBrightness();

    public partial void SetBrightness(double brightness);

    public partial void ResetBrightness();

    private partial double ReadCurrentBrightness();
}
