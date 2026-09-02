using K7.Clients.Shared.Interfaces;
using K7.Shared;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.Shared.Services;

public static class DeviceInitializer
{
    private static readonly SemaphoreSlim InitGate = new(1, 1);

    public static async Task InitializeDeviceAsync(IServiceProvider services, string? userId = null)
    {
        await InitGate.WaitAsync();
        try
        {
            await InitializeDeviceCoreAsync(services, userId);
        }
        finally
        {
            InitGate.Release();
        }
    }

    private static async Task InitializeDeviceCoreAsync(IServiceProvider services, string? userId = null)
    {
        try
        {
            var deviceStorageService = services.GetRequiredService<IDeviceStorageService>();
            var existingDeviceId = deviceStorageService.Get(PreferenceKeys.DEVICE_ID);
            var attachedUserId = deviceStorageService.Get(PreferenceKeys.DEVICE_ATTACHED_USER_ID);

            if (string.IsNullOrEmpty(existingDeviceId))
            {
                existingDeviceId = await CreateNewDeviceAsync(services, deviceStorageService);
            }

            if (Guid.TryParse(existingDeviceId, out var parsedId))
            {
                if (string.IsNullOrEmpty(userId) || attachedUserId != userId)
                {
                    var deviceApiService = services.GetRequiredService<IDeviceApiService>();

                    try
                    {
                        await deviceApiService.AttachCurrentUserToDeviceAsync(parsedId);
                        if (!string.IsNullOrEmpty(userId))
                        {
                            deviceStorageService.Set(PreferenceKeys.DEVICE_ATTACHED_USER_ID, userId);
                        }
                    }
                    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        deviceStorageService.Remove(PreferenceKeys.DEVICE_ID);
                        deviceStorageService.Remove(PreferenceKeys.DEVICE_ATTACHED_USER_ID);
                        existingDeviceId = await CreateNewDeviceAsync(services, deviceStorageService);
                        if (!Guid.TryParse(existingDeviceId, out parsedId))
                            return;
                    }
                }

                await RefreshDeviceCapabilitiesAsync(services, parsedId);
            }
        }
        catch (HttpRequestException)
        {
            // Not authenticated yet - device will be initialized after login
        }
        catch (InvalidOperationException)
        {
            // WebView JS runtime not ready yet on Windows - capabilities refresh retries next launch.
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
        PersistDeviceName(deviceStorageService, request.DeviceName);
        return deviceIdStr;
    }

    public static async Task RefreshDeviceCapabilitiesAsync(IServiceProvider services, Guid deviceId)
    {
        var deviceService = services.GetRequiredService<IDeviceService>();
        var deviceApiService = services.GetRequiredService<IDeviceApiService>();
        var deviceStorageService = services.GetRequiredService<IDeviceStorageService>();
        var createRequest = await deviceService.GenerateCreateDeviceRequestAsync();

        var updateRequest = new UpdateDeviceRequest
        {
            DeviceName = createRequest.DeviceName,
            ClientType = createRequest.ClientType,
            DeviceType = createRequest.DeviceType,
            OperatingSystem = createRequest.OperatingSystem,
            OperatingSystemVersion = createRequest.OperatingSystemVersion,
            DisplayHeight = createRequest.DisplayHeight,
            DisplayWidth = createRequest.DisplayWidth,
            NativeDeviceDetails = createRequest.NativeDeviceDetails,
            WebDeviceDetails = createRequest.WebDeviceDetails,
            PlaybackCapabilities = createRequest.PlaybackCapabilities
        };

        await deviceApiService.UpdateDeviceAsync(deviceId, updateRequest);
        PersistDeviceName(deviceStorageService, createRequest.DeviceName);
    }

    private static void PersistDeviceName(IDeviceStorageService deviceStorageService, string? deviceName)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return;

        deviceStorageService.Set(PreferenceKeys.DEVICE_NAME, deviceName.Trim());
    }
}
