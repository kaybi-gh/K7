using K7.Clients.Shared.Domain.Interfaces;
using Microsoft.JSInterop;

namespace K7.Clients.Web.Services;

public class DeviceService(IJSRuntime jsRuntime) : IDeviceService
{
    public string GetFormFactor()
    {
        return "Web";
    }

    public string GetPlatform()
    {
        return Environment.OSVersion.Platform.ToString();
    }

    public async Task<List<string>> GetSupportedCodecsAsync()
    {
        return await jsRuntime.InvokeAsync<List<string>>("getSupportedCodecs");
    }
}
