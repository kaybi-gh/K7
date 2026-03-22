using K7.Clients.Shared.Domain.Interfaces;
using K7.Shared;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components.Authorization;
using OpenIddict.Client;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using static OpenIddict.Abstractions.OpenIddictConstants;
using static OpenIddict.Abstractions.OpenIddictExceptions;

namespace K7.Clients.MAUI.Services.Authentication;

public class CustomAuthenticationStateProvider : AuthenticationStateProvider, ICustomAuthenticationStateProvider
{
    private readonly OpenIddictClientService _openIddictClientService;
    private readonly IK7ServerService _k7ServerService;
    private readonly IDeviceStorageService _deviceStorageService;
    private ClaimsPrincipal _currentUser = new(new ClaimsIdentity());
    private bool _initialized;

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
        if (!_initialized)
        {
            _initialized = true;
            TryRestoreSessionSync();
        }
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
                _deviceStorageService.Set(PreferenceKeys.ACCESS_TOKEN, result.BackchannelAccessToken);
            }

            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                _deviceStorageService.Set(PreferenceKeys.REFRESH_TOKEN, result.RefreshToken);
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

    public async Task LoginAsGuestAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _k7ServerService.HttpClient.PostAsync("api/authentication/guest-login", null, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
                return;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("access_token", out var tokenProp))
            {
                var accessToken = tokenProp.GetString()!;
                _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
                _deviceStorageService.Set(PreferenceKeys.ACCESS_TOKEN, accessToken);

                var parts = accessToken.Split('.');
                if (parts.Length == 3)
                {
                    var payload = parts[1].Replace('-', '+').Replace('_', '/');
                    switch (payload.Length % 4)
                    {
                        case 2: payload += "=="; break;
                        case 3: payload += "="; break;
                    }
                    var payloadJson = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
                    using var payloadDoc = JsonDocument.Parse(payloadJson);
                    var claims = new List<Claim>();
                    foreach (var prop in payloadDoc.RootElement.EnumerateObject())
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                            foreach (var item in prop.Value.EnumerateArray())
                                claims.Add(new Claim(prop.Name, item.GetString() ?? ""));
                        else if (prop.Value.ValueKind == JsonValueKind.String)
                            claims.Add(new Claim(prop.Name, prop.Value.GetString()!));
                        else if (prop.Value.ValueKind == JsonValueKind.Number)
                            claims.Add(new Claim(prop.Name, prop.Value.GetRawText()));
                    }
                    _currentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "OpenIddict"));
                }
            }
        }
        catch
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
            _deviceStorageService.Set(PreferenceKeys.ACCESS_TOKEN, result.AccessToken);
        }

        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            _deviceStorageService.Set(PreferenceKeys.REFRESH_TOKEN, result.RefreshToken);
        }

        await TryAttachCurrentUserToDeviceAsync(cancellationToken);

        NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
        _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization = null;
        _deviceStorageService.Remove(PreferenceKeys.ACCESS_TOKEN);
        _deviceStorageService.Remove(PreferenceKeys.REFRESH_TOKEN);

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

    private void TryRestoreSessionSync()
    {
        try
        {
            var refreshToken = _deviceStorageService.Get(PreferenceKeys.REFRESH_TOKEN);
            if (string.IsNullOrEmpty(refreshToken))
            {
                return;
            }

            _ = RefreshInBackgroundAsync();
        }
        catch
        {
            ClearStoredTokens();
        }
    }

    private async Task RefreshInBackgroundAsync()
    {
        if (await TryRefreshTokenAsync())
        {
            NotifyAuthenticationStateChanged(
                Task.FromResult(new AuthenticationState(_currentUser)));
        }
        else
        {
            ClearStoredTokens();
        }
    }

    private async Task<bool> TryRefreshTokenAsync()
    {
        var refreshToken = _deviceStorageService.Get(PreferenceKeys.REFRESH_TOKEN);
        if (string.IsNullOrEmpty(refreshToken))
        {
            return false;
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            var result = await _openIddictClientService.AuthenticateWithRefreshTokenAsync(new()
            {
                CancellationToken = cts.Token,
                RefreshToken = refreshToken,
                ProviderName = "K7"
            });

            _currentUser = new ClaimsPrincipal(new ClaimsIdentity(result.Principal.Claims, "OpenIddict"));

            if (!string.IsNullOrEmpty(result.AccessToken))
            {
                _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", result.AccessToken);
                _deviceStorageService.Set(PreferenceKeys.ACCESS_TOKEN, result.AccessToken);
            }

            if (!string.IsNullOrEmpty(result.RefreshToken))
            {
                _deviceStorageService.Set(PreferenceKeys.REFRESH_TOKEN, result.RefreshToken);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }


    private void ClearStoredTokens()
    {
        _deviceStorageService.Remove(PreferenceKeys.ACCESS_TOKEN);
        _deviceStorageService.Remove(PreferenceKeys.REFRESH_TOKEN);
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
