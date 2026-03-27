using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.Shared.Services;
public static class DeviceInitializer
{
    public static async Task InitializeDeviceAsync(IServiceProvider services)
    {
        try
        {
            var deviceStorageService = services.GetRequiredService<IDeviceStorageService>();
            var existingDeviceId = deviceStorageService.Get(PreferenceKeys.DEVICE_ID);

            if (string.IsNullOrEmpty(existingDeviceId))
            {
                var deviceService = services.GetRequiredService<IDeviceService>();
                var deviceApiService = services.GetRequiredService<IDeviceApiService>();
                var request = await deviceService.GenerateCreateDeviceRequestAsync();
                var deviceId = await deviceApiService.CreateDeviceAsync(request);
                deviceStorageService.Set(PreferenceKeys.DEVICE_ID, deviceId.ToString());
                existingDeviceId = deviceId.ToString();
            }

            if (Guid.TryParse(existingDeviceId, out var parsedId))
            {
                var deviceApiService = services.GetRequiredService<IDeviceApiService>();
                await deviceApiService.AttachCurrentUserToDeviceAsync(parsedId);
            }
        }
        catch (HttpRequestException)
        {
            // Not authenticated yet � device will be initialized after login
        }
    }
}
