using K7.Clients.Shared.Domain.Interfaces;

namespace K7.Clients.Web.Client.Services;

public class FormFactorService : IFormFactorService
{
    public string GetFormFactor()
    {
        return "WebAssembly";
    }

    public string GetPlatform()
    {
        return Environment.OSVersion.ToString();
    }
}
