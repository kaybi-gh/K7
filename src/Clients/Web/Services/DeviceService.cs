using K7.Clients.Shared.Domain.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using Microsoft.JSInterop;
using OperatingSystem = K7.Server.Domain.Enums.OperatingSystem;

namespace K7.Clients.Web.Services;

public class DeviceService(IJSRuntime jsRuntime) : IDeviceService
{
    public async Task<CreateDeviceRequest> GenerateCreateDeviceRequestAsync()
    {
        var displayHeight = await jsRuntime.InvokeAsync<int>("getDisplayHeight");
        var displayWidth = await jsRuntime.InvokeAsync<int>("getDisplayWidth");
        var supportedMediaFormats = await GetSupportedMediaFormatsAsync();

        return new CreateDeviceRequest
        {
            DeviceName = "toto",
            DeviceType = await GetDeviceTypeAsync(),
            OperatingSystem = GetOperatingSystem(),
            DisplayHeight = displayHeight,
            DisplayWidth = displayWidth,
            SupportedMediaFormatIds = supportedMediaFormats.Select(x => x.Id),
            SupportsHDR = await GetHdrSupportAsync()
        };
    }

    public string? GetDeviceId()
    {
        throw new NotImplementedException();
    }

    public async Task<DeviceType> GetDeviceTypeAsync()
    {
        var isDesktop = Environment.OSVersion.Platform switch
        {
            PlatformID.Win32S => true,
            PlatformID.Win32Windows => true,
            PlatformID.Win32NT => true,
            PlatformID.WinCE => true,
            PlatformID.Unix => true,
            PlatformID.Xbox => true,
            PlatformID.MacOSX => true,
            PlatformID.Other => false,
            _ => false
        };

        if (isDesktop)
        {
            return DeviceType.Desktop;
        }

        var deviceType = await jsRuntime.InvokeAsync<string>("getDeviceType");
        return deviceType switch
        {
            "Mobile" => DeviceType.Phone,
            "Tablet" => DeviceType.Tablet,
            "Desktop" => DeviceType.Desktop,
            "TV" => DeviceType.TV,
            _ => DeviceType.Unknown
        };
    }

    public OperatingSystem GetOperatingSystem()
    {
        return OperatingSystem.Browser;
    }

    public async Task<List<MediaFormatDto>> GetSupportedMediaFormatsAsync()
    {
        return await jsRuntime.InvokeAsync<List<MediaFormatDto>>("getSupportedMediaFormatsAsync");
    }

    public async Task<bool> GetHdrSupportAsync()
    {
        return await jsRuntime.InvokeAsync<bool>("getHdrSupport");
    }

    public string GetPlatform()
    {
        return Environment.OSVersion.ToString();
    }
}
