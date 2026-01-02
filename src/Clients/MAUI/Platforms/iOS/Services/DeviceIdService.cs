using K7.Clients.MAUI.Interfaces;
using UIKit;

namespace K7.Clients.MAUI.Platforms.iOS.Services;

public class DeviceIdService : IDeviceIdService
{
    public string? GetDeviceId()
    {
        return UIDevice.CurrentDevice.IdentifierForVendor?.ToString();
    }
}
