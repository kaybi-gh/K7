using K7.Clients.Shared.Domain.Interfaces;
using K7.Shared;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
using OpenIddict.Client;
using System.Net.Http.Headers;
using System.Security.Claims;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Abstractions.OpenIddictExceptions;

namespace K7.Clients.MAUI.Services.Authentication;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider, ICustomAuthenticationStateProvider
{
    private readonly OpenIddictClientService _openIddictClientService;
    private readonly IK7ServerService _k7ServerService;
    private readonly IDeviceStorageService _deviceStorageService;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());

    public CustomAuthenticationStateProvider(
        OpenIddictClientService openIddictClientService,
        IK7ServerService k7ServerService,
        IDeviceStorageService deviceStorageService)
    {
        _openIddictClientService = openIddictClientService;
        _k7ServerService = k7ServerService;
        _deviceStorageService = deviceStorageService;
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        return Task.FromResult(new AuthenticationState(_currentUser));
    }

    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));

        try
        {
            var challenge = await _openIddictClientService.ChallengeInteractivelyAsync(new()
            {
                CancellationToken = timeout.Token,
                ProviderName = "K7"
            });

            var result = await _openIddictClientService.AuthenticateInteractivelyAsync(new()
            {
                CancellationToken = timeout.Token,
                Nonce = challenge.Nonce
            });

            _currentUser = new ClaimsPrincipal(new ClaimsIdentity(result.Principal.Claims, "OpenIddict"));

            if (!string.IsNullOrEmpty(result.BackchannelAccessToken))
            {
                _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", result.BackchannelAccessToken);
            }

            await TryAttachCurrentUserToDeviceAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }
        catch (ProtocolException exception) when (exception.Error is Errors.AccessDenied)
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        }

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task LoginWithDeviceCodeAsync(Func<DeviceCodeInfo, Task> onDeviceCodeReceived, CancellationToken cancellationToken = default)
    {
        var challenge = await _openIddictClientService.ChallengeUsingDeviceAsync(new()
        {
            CancellationToken = cancellationToken,
            ProviderName = "K7"
        });

        await onDeviceCodeReceived(new DeviceCodeInfo(
            challenge.UserCode,
            challenge.VerificationUri.ToString(),
            challenge.VerificationUriComplete?.ToString() ?? challenge.VerificationUri + "?user_code=" + Uri.EscapeDataString(challenge.UserCode),
            DateTimeOffset.UtcNow.Add(challenge.ExpiresIn)));

        var result = await _openIddictClientService.AuthenticateWithDeviceAsync(new()
        {
            CancellationToken = cancellationToken,
            DeviceCode = challenge.DeviceCode,
            Interval = challenge.Interval,
            Timeout = challenge.ExpiresIn,
            ProviderName = "K7"
        });

        _currentUser = new ClaimsPrincipal(new ClaimsIdentity(result.Principal.Claims, "OpenIddict"));

        if (!string.IsNullOrEmpty(result.AccessToken))
        {
            _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", result.AccessToken);
        }

        await TryAttachCurrentUserToDeviceAsync(cancellationToken);

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization = null;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(30));

            var result = await _openIddictClientService.SignOutInteractivelyAsync(new()
            {
                CancellationToken = timeout.Token,
                ProviderName = "K7"
            });

            await _openIddictClientService.AuthenticateInteractivelyAsync(new()
            {
                CancellationToken = timeout.Token,
                Nonce = result.Nonce
            });
        }
        catch { }

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
