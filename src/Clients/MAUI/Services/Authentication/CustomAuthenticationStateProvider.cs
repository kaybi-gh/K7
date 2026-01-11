using K7.Clients.MAUI.Interfaces;
using K7.Clients.Shared.Domain.Interfaces;
using K7.Shared;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Identity.Client;
using System.Security.Claims;

namespace K7.Clients.MAUI.Services.Authentication;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider, ICustomAuthenticationStateProvider
{
    private readonly IMsalClientService _msalClientService;
    private readonly IK7ServerService _k7ServerService;
    private readonly IDeviceStorageService _deviceStorageService;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public CustomAuthenticationStateProvider(IMsalClientService msalClientService, IK7ServerService k7ServerService, IDeviceStorageService deviceStorageService)
    {
        _msalClientService = msalClientService;
        _k7ServerService = k7ServerService;
        _deviceStorageService = deviceStorageService;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(_currentUser));
    }

    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        var msalClient = _msalClientService.GetClient();

        string[] scopes = ["openid", "profile", "email", "api"];

        AuthenticationResult? result;
        try
        {
            var accounts = await msalClient.GetAccountsAsync();
            result = await msalClient.AcquireTokenSilent(scopes, accounts.FirstOrDefault())
                .ExecuteAsync();
        }
        catch (MsalUiRequiredException)
        {
            var options = new SystemWebViewOptions()
            {
                HtmlMessageError = "<p> An error occurred: {0}. Details {1}</p>",
                BrowserRedirectSuccess = new Uri("https://www.microsoft.com")
            };

            try
            {
                result = await msalClient.AcquireTokenInteractive(scopes)
                        .WithUseEmbeddedWebView(false)
                        .WithSystemWebViewOptions(new SystemWebViewOptions())
                        .ExecuteAsync(cancellationToken);
            }
            catch
            {
                result = null;
            }
        }

        /*var tata = await _app.AcquireTokenWithDeviceCode(["openid", "profile", "email", "api"], deviceCodeResult =>
        {
            // This will print the message on the console which tells the user where to go sign-in using 
            // a separate browser and the code to enter once they sign in.
            // The AcquireTokenWithDeviceCode() method will poll the server after firing this
            // device code callback to look for the successful login of the user via that browser.
            // This background polling (whose interval and timeout data is also provided as fields in the 
            // deviceCodeCallback class) will occur until:
            // * The user has successfully logged in via browser and entered the proper code
            // * The timeout specified by the server for the lifetime of this code (typically ~15 minutes) has been reached
            // * The developing application calls the Cancel() method on a CancellationToken sent into the method.
            //   If this occurs, an OperationCanceledException will be thrown (see catch below for more details).
            Console.WriteLine(deviceCodeResult.Message);
            return Task.FromResult(0);
        }).ExecuteAsync();*/

        if (result != null)
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity(result.ClaimsPrincipal.Claims, "AuthenticationTypes.Federation"));
            await TryAttachCurrentUserToDeviceAsync(cancellationToken);
        }
        else
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }
        
        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        var msalClient = _msalClientService.GetClient();

        var accounts = await msalClient.GetAccountsAsync();
        foreach (var account in accounts)
        {
            await msalClient.RemoveAsync(account);
        }

        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));
    }

    private async Task TryAttachCurrentUserToDeviceAsync(CancellationToken cancellationToken)
    {
        try
        {
            var storedDeviceId = _deviceStorageService.Get(PreferenceKeys.DEVICE_ID);
            if (Guid.TryParse(storedDeviceId, out var parsedId))
            {
                await _k7ServerService.AttachCurrentUserToDeviceAsync(parsedId, cancellationToken);
            }
        }
        catch { }
    }
}
