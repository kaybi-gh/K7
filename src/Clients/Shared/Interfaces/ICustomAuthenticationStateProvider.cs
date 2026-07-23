using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface ICustomAuthenticationStateProvider
{
    Task LoginAsync(CancellationToken cancellationToken = default);
    Task LoginAsGuestAsync(CancellationToken cancellationToken = default);
    Task LoginWithDeviceCodeAsync(Func<DeviceCodeInfo, Task> onDeviceCodeReceived, CancellationToken cancellationToken = default);
    /// <param name="rejectedAccessToken">
    /// When set (e.g. after a 401), force a refresh-token grant if this is still the stored access token.
    /// </param>
    Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default, string? rejectedAccessToken = null);
    /// <summary>
    /// Activates a stored local profile by redeeming its refresh token.
    /// </summary>
    /// <param name="identityUserId">The Identity user id of the LocalUser to switch to.</param>
    Task<bool> SwitchToUserAsync(string identityUserId, CancellationToken cancellationToken = default);
    void SignInOffline(LocalUser user);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}

public record DeviceCodeInfo(
    string UserCode,
    string VerificationUri,
    string VerificationUriComplete,
    DateTimeOffset ExpiresOn);
