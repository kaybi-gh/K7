using MediaClient.Shared.Domain.Enums;
using Microsoft.JSInterop;

namespace MediaClient.Shared.Services;

public class CurrentDeviceService : ICurrentDeviceService
{
    private readonly IJSRuntime _jsRuntime;

    public CurrentDeviceService(IJSRuntime jSRuntime)
    {
        _jsRuntime = jSRuntime;
    }

    public async ValueTask<DeviceType> GetDeviceTypeAsync()
    {
        var deviceType = await _jsRuntime.InvokeAsync<string>("eval", "device.type");
        return deviceType switch
        {
            "mobile" => DeviceType.Mobile,
            "tablet" => DeviceType.Tablet,
            "desktop" => DeviceType.Desktop,
            _ => DeviceType.Unknown
        };
    }

    public async ValueTask<DeviceOrientation> GetDeviceOrientationAsync()
    {
        var deviceOrientation = await _jsRuntime.InvokeAsync<string>("eval", "device.orientation");
        return deviceOrientation switch
        {
            "landscape" => DeviceOrientation.Landscape,
            "portrait" => DeviceOrientation.Portrait,
            _ => DeviceOrientation.Unknown
        };
    }

    public async ValueTask<DeviceOS> GetDeviceOSAsync()
    {
        var deviceOS = await _jsRuntime.InvokeAsync<string>("eval", "device.orientation");
        return deviceOS switch
        {
            "Android" => DeviceOS.Android,
            "Blackberry" => DeviceOS.Blackberry,
            "FXOS" => DeviceOS.FXOS,
            "iOS" => DeviceOS.iOS,
            "iPad" => DeviceOS.iPad,
            "iPhone" => DeviceOS.iPhone,
            "iPod" => DeviceOS.iPod,
            "macOS" => DeviceOS.macOS,
            "MeeGo" => DeviceOS.MeeGo,
            "Television" => DeviceOS.Television,
            "Windows" => DeviceOS.Windows,
            _ => DeviceOS.Unknown
        };
    }
}
