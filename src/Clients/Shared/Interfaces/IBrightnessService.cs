namespace K7.Clients.Shared.Interfaces;

public interface IBrightnessService
{
    bool SupportsNativeBrightness { get; }
    double Brightness { get; }
    void SetBrightness(double brightness);
    void ResetBrightness();
}
