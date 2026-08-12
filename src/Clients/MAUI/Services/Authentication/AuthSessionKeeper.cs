using System.IdentityModel.Tokens.Jwt;
using K7.Clients.Shared.Interfaces;
using K7.Shared;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace K7.Clients.MAUI.Services.Authentication;

/// <summary>
/// Quietly refreshes the access token on app resume and near expiry so long-lived native
/// players (ExoPlayer) can rebind before baked Authorization headers start returning 401.
/// HttpClient refresh alone is not enough: MediaElement/ExoPlayer keep the Bearer from open.
/// </summary>
public sealed class AuthSessionKeeper : IDisposable
{
    private static readonly TimeSpan ProactiveRefreshSkew = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(2);

    private readonly ICustomAuthenticationStateProvider _auth;
    private readonly IDeviceStorageService _deviceStorage;
    private readonly ILogger<AuthSessionKeeper> _logger;
    private readonly Timer _pollTimer;
    private int _refreshInFlight;
    private bool _disposed;

    public AuthSessionKeeper(
        ICustomAuthenticationStateProvider auth,
        IDeviceStorageService deviceStorage,
        ILogger<AuthSessionKeeper> logger)
    {
        _auth = auth;
        _deviceStorage = deviceStorage;
        _logger = logger;
        _pollTimer = new Timer(
            _ => _ = RefreshIfNearExpiryAsync(),
            null,
            PollInterval,
            PollInterval);
    }

    public void OnAppResumed()
    {
        _ = RefreshQuietlyAsync(forceRefresh: false);
    }

    private Task RefreshIfNearExpiryAsync()
    {
        if (!IsAccessTokenNearExpiry())
            return Task.CompletedTask;

        return RefreshQuietlyAsync(forceRefresh: true);
    }

    private async Task RefreshQuietlyAsync(bool forceRefresh)
    {
        if (Interlocked.CompareExchange(ref _refreshInFlight, 1, 0) != 0)
            return;

        try
        {
            // Wait for cold-start restore so we never race it on /connect/token.
            if (_auth is AuthenticationStateProvider stateProvider)
                await stateProvider.GetAuthenticationStateAsync();

            if (string.IsNullOrEmpty(_deviceStorage.Get(PreferenceKeys.REFRESH_TOKEN)))
                return;

            await _auth.TryRefreshAsync(forceRefresh: forceRefresh);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Proactive auth refresh failed");
        }
        finally
        {
            Interlocked.Exchange(ref _refreshInFlight, 0);
        }
    }

    private bool IsAccessTokenNearExpiry()
    {
        var accessToken = _deviceStorage.Get(PreferenceKeys.ACCESS_TOKEN);
        if (string.IsNullOrEmpty(accessToken))
            return false;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(accessToken))
                return false;

            var jwt = handler.ReadJwtToken(accessToken);
            return jwt.ValidTo <= DateTime.UtcNow.Add(ProactiveRefreshSkew);
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _pollTimer.Dispose();
    }
}
