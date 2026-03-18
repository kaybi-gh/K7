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

    public async Task LoginWithDeviceCodeAsync(Func<DeviceCodeInfo, Task> onDeviceCodeReceived, CancellationToken cancellationToken = default)
    {
        var msalClient = _msalClientService.GetClient();
        string[] scopes = ["openid", "profile", "email", "api"];

        var result = await msalClient.AcquireTokenWithDeviceCode(scopes, async deviceCodeResult =>
        {
            await onDeviceCodeReceived(new DeviceCodeInfo(
                deviceCodeResult.UserCode,
                deviceCodeResult.VerificationUrl,
                deviceCodeResult.VerificationUrl + "?user_code=" + Uri.EscapeDataString(deviceCodeResult.UserCode),
                deviceCodeResult.ExpiresOn));
        }).ExecuteAsync(cancellationToken);

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
