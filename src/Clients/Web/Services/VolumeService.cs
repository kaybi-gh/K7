using K7.Clients.Shared.Interfaces;

namespace K7.Clients.Web.Services;

public class VolumeService : IVolumeService
{
    public bool SupportsNativeVolume => false;

    public double Volume => 1.0;

    public void SetVolume(double volume)
    {
        // Web: volume is controlled via PlayerService
    }
}
