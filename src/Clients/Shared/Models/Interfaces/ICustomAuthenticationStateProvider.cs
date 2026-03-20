using System.Security.Claims;

namespace K7.Clients.Shared.Domain.Interfaces;

public interface ICustomAuthenticationStateProvider
{
    Task LoginAsync(CancellationToken cancellationToken = default);
    Task LoginAsGuestAsync(CancellationToken cancellationToken = default);
    Task LoginWithDeviceCodeAsync(Func<DeviceCodeInfo, Task> onDeviceCodeReceived, CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
}

public record DeviceCodeInfo(
    string UserCode,
    string VerificationUri,
    string VerificationUriComplete,
    DateTimeOffset ExpiresOn);
