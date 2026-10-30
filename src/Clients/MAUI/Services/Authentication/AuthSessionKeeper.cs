using K7.Clients.Shared.Helpers;
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
            if (_auth is AuthenticationStateProvider stateProvider)
            {
                var state = await stateProvider.GetAuthenticationStateAsync().ConfigureAwait(false);
                if (state.User.Identity?.IsAuthenticated != true)
                    return;

                if (!forceRefresh && !IsAccessTokenNearExpiry())
                    return;
            }

            if (string.IsNullOrEmpty(_deviceStorage.Get(PreferenceKeys.REFRESH_TOKEN)))
                return;

            await _auth.TryRefreshAsync(forceRefresh: forceRefresh).ConfigureAwait(false);
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

        // Stored expiry first: older servers issue encrypted access tokens, so "exp" may be unreadable and
        // the old JWT-only check saw DateTime.MinValue (a forced refresh on every poll).
        var expiresAt = StoredSession.ParseExpiry(_deviceStorage.Get(PreferenceKeys.ACCESS_TOKEN_EXPIRES_AT))
            ?? JwtAccessToken.TryReadExpiry(accessToken);
        return StoredSession.IsAccessTokenNearExpiry(expiresAt, DateTimeOffset.UtcNow, ProactiveRefreshSkew);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _pollTimer.Dispose();
    }
}
