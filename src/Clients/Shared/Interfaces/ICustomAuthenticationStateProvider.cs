using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface ICustomAuthenticationStateProvider
{
    Task LoginAsync(CancellationToken cancellationToken = default);
    Task LoginAsGuestAsync(CancellationToken cancellationToken = default);
    Task LoginWithDeviceCodeAsync(Func<DeviceCodeInfo, Task> onDeviceCodeReceived, CancellationToken cancellationToken = default);
    Task<bool> TryRefreshAsync(CancellationToken cancellationToken = default);
    Task<bool> SwitchToUserAsync(string refreshToken, CancellationToken cancellationToken = default);
    void SignInOffline(LocalUser user);
    Task RefreshStoredUserProfilesAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}

public record DeviceCodeInfo(
    string UserCode,
    string VerificationUri,
    string VerificationUriComplete,
    DateTimeOffset ExpiresOn);
