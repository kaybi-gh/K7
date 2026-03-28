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
                existingDeviceId = await CreateNewDeviceAsync(services, deviceStorageService);
            }

            if (Guid.TryParse(existingDeviceId, out var parsedId))
            {
                var deviceApiService = services.GetRequiredService<IDeviceApiService>();

                try
                {
                    await deviceApiService.AttachCurrentUserToDeviceAsync(parsedId);
                }
                catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    deviceStorageService.Remove(PreferenceKeys.DEVICE_ID);
                    await CreateNewDeviceAsync(services, deviceStorageService);
                }
            }
        }
        catch (HttpRequestException)
        {
            // Not authenticated yet — device will be initialized after login
        }
    }

    private static async Task<string> CreateNewDeviceAsync(IServiceProvider services, IDeviceStorageService deviceStorageService)
    {
        var deviceService = services.GetRequiredService<IDeviceService>();
        var deviceApiService = services.GetRequiredService<IDeviceApiService>();
        var request = await deviceService.GenerateCreateDeviceRequestAsync();
        var deviceId = await deviceApiService.CreateDeviceAsync(request);
        var deviceIdStr = deviceId.ToString();
        deviceStorageService.Set(PreferenceKeys.DEVICE_ID, deviceIdStr);
        return deviceIdStr;
    }
}
