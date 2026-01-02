using Microsoft.Identity.Client;

namespace K7.Clients.MAUI.Interfaces;

public interface IMsalClientService
{
    public void Initialize(string oidcAuthorityUrl);
    public IPublicClientApplication GetClient();
}
