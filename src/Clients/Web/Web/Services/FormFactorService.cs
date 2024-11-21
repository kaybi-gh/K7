using K7.Clients.Shared.Domain.Interfaces;

namespace K7.Clients.Web.Services;

public class FormFactorService : IFormFactorService
{
    public string GetFormFactor()
    {
        return "Web";
    }

    public string GetPlatform()
    {
        return Environment.OSVersion.ToString();
    }
}
