using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Helpers;

public static class ConnectedDeviceLabels
{
    public static string GetDisplayName(ConnectedDeviceDto device)
    {
        if (!string.IsNullOrWhiteSpace(device.DeviceName))
            return device.DeviceName.Trim();

        if (!string.IsNullOrWhiteSpace(device.DeviceType))
            return device.DeviceType.Trim();

        return "Device";
    }
}
