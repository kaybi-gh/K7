using K7.Clients.Shared.Domain.Interfaces;
using Microsoft.JSInterop;

namespace K7.Clients.Web.Client.Services;

public class DeviceService(IJSRuntime jsRuntime) : IDeviceService
{
    public string GetFormFactor()
    {
        return "WebAssembly";
    }

    public string GetPlatform()
    {
        return Environment.OSVersion.ToString();
    }

    public async Task<List<string>> GetSupportedCodecsAsync()
    {
        return await jsRuntime.InvokeAsync<List<string>>("getSupportedCodecs");
    }
}
