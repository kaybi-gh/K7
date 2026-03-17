using System.Diagnostics;
using K7.Clients.MAUI.Interfaces;
using Microsoft.Identity.Client;

namespace K7.Clients.MAUI.Services.Authentication;
public class MsalClientService : IMsalClientService
{
    private IPublicClientApplication? _msalClient;

    public void Initialize(string oidcAuthorityUrl)
    {
        if (string.IsNullOrEmpty(oidcAuthorityUrl))
        {
            throw new NullReferenceException(oidcAuthorityUrl);
        }

        _msalClient = PublicClientApplicationBuilder.Create("k7-native")
            .WithExperimentalFeatures()
            .WithOidcAuthority(oidcAuthorityUrl)
            .WithRedirectUri("http://localhost:59451")
#if DEBUG
            .WithLogging((level, message, containsPii) =>
            {
                Debug.WriteLine($"[MSAL] {level}: {message}");
            }, LogLevel.Verbose, enablePiiLogging: true, enableDefaultPlatformLogging: true)
#endif
            .Build();
    }

    public IPublicClientApplication GetClient()
    {
        if (_msalClient == null)
        {
            throw new InvalidOperationException("MSAL client is not initialized.");
        }
        return _msalClient;
    }
}
