using K7.Clients.Shared.Domain.Interfaces;
using K7.Shared;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.Shared.Services;
public static class DeviceInitializer
{
    public static async Task InitializeDeviceAsync(IServiceProvider services)
    {
        var deviceStorageService = services.GetRequiredService<IDeviceStorageService>();
        if (string.IsNullOrEmpty(deviceStorageService.Get(PreferenceKeys.DEVICE_ID)))
        {
            var deviceService = services.GetRequiredService<IDeviceService>();
            var k7ServerService = services.GetRequiredService<IK7ServerService>();
            var request = await deviceService.GenerateCreateDeviceRequestAsync();
            var deviceId = await k7ServerService.CreateDeviceAsync(request);
            deviceStorageService.Set(PreferenceKeys.DEVICE_ID, deviceId.ToString());
        }
        else
        {
            // TODO - Update device capabilities
        }
    }
}
