using K7.Clients.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Clients.MAUI.Services.Authentication;

/// <summary>
/// Quietly refreshes the access token when the app returns to the foreground so the first
/// user action after a long sleep does not race expired Bearer tokens.
/// </summary>
public sealed class AuthSessionKeeper
{
    private readonly ICustomAuthenticationStateProvider _auth;
    private readonly ILogger<AuthSessionKeeper> _logger;
    private int _refreshInFlight;

    public AuthSessionKeeper(ICustomAuthenticationStateProvider auth, ILogger<AuthSessionKeeper> logger)
    {
        _auth = auth;
        _logger = logger;
    }

    public void OnAppResumed()
    {
        if (Interlocked.CompareExchange(ref _refreshInFlight, 1, 0) != 0)
            return;

        _ = RefreshQuietlyAsync();
    }

    private async Task RefreshQuietlyAsync()
    {
        try
        {
            await _auth.TryRefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Proactive auth refresh on resume failed");
        }
        finally
        {
            Interlocked.Exchange(ref _refreshInFlight, 0);
        }
    }
}
