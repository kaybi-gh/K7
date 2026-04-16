namespace K7.Clients.Shared.Interfaces;

public interface IBrightnessService
{
    double Brightness { get; }
    void SetBrightness(double brightness);
    void ResetBrightness();
}
