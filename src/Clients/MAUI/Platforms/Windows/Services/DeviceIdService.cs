using K7.Clients.MAUI.Interfaces;

namespace K7.Clients.MAUI.Platforms.Windows.Services;

public class DeviceIdService : IDeviceIdService
{
    public string? GetDeviceId()
    {
        // No available implementation on Windows
        return null;
    }
}
