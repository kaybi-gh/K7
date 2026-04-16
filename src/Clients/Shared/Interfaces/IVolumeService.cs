namespace K7.Clients.Shared.Interfaces;

public interface IVolumeService
{
    bool SupportsNativeVolume { get; }
    double Volume { get; }
    void SetVolume(double volume);
}
