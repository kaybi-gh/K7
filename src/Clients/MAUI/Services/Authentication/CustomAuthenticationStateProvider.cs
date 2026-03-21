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
            var storedToken = _deviceStorageService.Get(PreferenceKeys.ACCESS_TOKEN);
            if (string.IsNullOrEmpty(storedToken))
                return;

            var claims = DecodeJwtClaims(storedToken);
            if (claims is null)
            {
                ClearStoredTokens();
                return;
            }

            var expClaim = claims.FirstOrDefault(c => c.Type == "exp");
            bool isExpired = expClaim is not null
                && long.TryParse(expClaim.Value, out var expUnix)
                && DateTimeOffset.FromUnixTimeSeconds(expUnix) < DateTimeOffset.UtcNow;

            if (isExpired)
            {
                _ = RefreshInBackgroundAsync();
                return;
            }

            _currentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "OpenIddict"));
            _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", storedToken);
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
            return false;

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

            using var request = new HttpRequestMessage(HttpMethod.Post, "connect/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["client_id"] = "k7-native"
                })
            };

            var savedAuth = _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization;
            _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization = null;

            var response = await _k7ServerService.HttpClient.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization = savedAuth;
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("access_token", out var accessTokenProp))
                return false;

            var newAccessToken = accessTokenProp.GetString();
            if (string.IsNullOrEmpty(newAccessToken))
                return false;

            var claims = DecodeJwtClaims(newAccessToken);
            if (claims is null)
                return false;

            _currentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "OpenIddict"));
            _k7ServerService.HttpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", newAccessToken);
            _deviceStorageService.Set(PreferenceKeys.ACCESS_TOKEN, newAccessToken);

            if (root.TryGetProperty("refresh_token", out var refreshTokenProp))
            {
                var newRefreshToken = refreshTokenProp.GetString();
                if (!string.IsNullOrEmpty(newRefreshToken))
                    _deviceStorageService.Set(PreferenceKeys.REFRESH_TOKEN, newRefreshToken);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static List<Claim>? DecodeJwtClaims(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 3)
            return null;

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        switch (payload.Length % 4)
        {
            case 2: payload += "=="; break;
            case 3: payload += "="; break;
        }

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(payload));
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var claims = new List<Claim>();
        foreach (var prop in root.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.Value.EnumerateArray())
                    claims.Add(new Claim(prop.Name, item.GetString() ?? ""));
            }
            else if (prop.Value.ValueKind == JsonValueKind.String)
            {
                claims.Add(new Claim(prop.Name, prop.Value.GetString()!));
            }
            else if (prop.Value.ValueKind == JsonValueKind.Number)
            {
                claims.Add(new Claim(prop.Name, prop.Value.GetRawText()));
            }
        }

        return claims;
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
