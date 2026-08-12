using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface ICustomAuthenticationStateProvider
{
    /// <summary>
    /// Raised when the stored access token value changes (login, refresh, logout clear is not raised).
    /// Native players that bake Authorization into ExoPlayer/MediaElement must rebind on this.
    /// </summary>
    event EventHandler? AccessTokenChanged;

    Task LoginAsync(CancellationToken cancellationToken = default);
    Task LoginAsGuestAsync(CancellationToken cancellationToken = default);
    Task LoginWithDeviceCodeAsync(Func<DeviceCodeInfo, Task> onDeviceCodeReceived, CancellationToken cancellationToken = default);
    /// <param name="rejectedAccessToken">
    /// When set (e.g. after a 401), force a refresh-token grant if this is still the stored access token.
    /// </param>
    /// <param name="forceRefresh">
    /// When true, always hit the token endpoint even if the current access token still looks valid.
    /// Used for proactive rotation before long-lived native players (ExoPlayer) keep a stale Bearer.
    /// </param>
    Task<bool> TryRefreshAsync(
        CancellationToken cancellationToken = default,
        string? rejectedAccessToken = null,
        bool forceRefresh = false);
    /// <summary>
    /// Activates a stored local profile by redeeming its refresh token.
    /// </summary>
    /// <param name="identityUserId">The Identity user id of the LocalUser to switch to.</param>
    Task<bool> SwitchToUserAsync(string identityUserId, CancellationToken cancellationToken = default);
    void SignInOffline(LocalUser user);
    /// <summary>
    /// Clears the online session without removing the local profile card (401 / revoked RT).
    /// </summary>
    Task EndSessionAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Signs out and removes the current profile from this device.
    /// </summary>
    Task LogoutAsync(CancellationToken cancellationToken = default);
}

public record DeviceCodeInfo(
    string UserCode,
    string VerificationUri,
    string VerificationUriComplete,
    DateTimeOffset ExpiresOn);
