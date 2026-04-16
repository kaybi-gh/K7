using K7.Clients.Shared.Interfaces;

namespace K7.Clients.MAUI.Services;

public partial class VolumeService : IVolumeService
{
    public partial bool SupportsNativeVolume { get; }

    public partial double Volume { get; }

    public partial void SetVolume(double volume);
}
